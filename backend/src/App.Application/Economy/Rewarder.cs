using App.Application.Seasons;
using App.Domain;
using App.Domain.Economy;
using App.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace App.Application.Economy;

// The only writer of the ledger (docs §3.7). Every grant is one reward_events row, once per dedupe key, and the cached
// users.coins / users.xp move with it in the caller's transaction. XP may cross levels and the weekly goal, which pay
// coins on their own keys.
public class Rewarder(IAppDbContext db, TimeProvider time)
{
    public async Task<bool> GrantAsync(Guid userId, Grant grant, CancellationToken ct)
    {
        if (grant is { Coins: 0, Xp: 0, Sp: 0 }) return false;
        var now = time.GetUtcNow();
        // SP count for the current season: the row carries the season and user_seasons.sp is its cached sum.
        var seasonId = grant.Sp != 0 ? (await db.CurrentSeasonAsync(now, ct)).Id : (Guid?)null;
        var inserted = await db.Database.ExecuteSqlAsync(
            $"""
            insert into reward_events (id, user_id, type, coins, xp, sp, season_id, reference_id, dedupe_key, created_at)
            values ({Guid.CreateVersion7()}, {userId}, {Rewards.Name(grant.Type)}, {grant.Coins}, {grant.Xp}, {grant.Sp}, {seasonId}, {grant.ReferenceId}, {grant.DedupeKey}, {now})
            on conflict (dedupe_key) do nothing
            """, ct);
        if (inserted == 0) return false;

        if (seasonId is { } season)
        {
            await db.JoinSeasonAsync(userId, season, ct);
            await db.UserSeasons.Where(s => s.UserId == userId && s.SeasonId == season)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Sp, x => x.Sp + grant.Sp), ct);
        }

        var oldXp = await db.Users.Where(u => u.Id == userId).Select(u => u.Xp).FirstAsync(ct);
        if (grant.Coins != 0 || grant.Xp != 0)
            await db.Users.Where(u => u.Id == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Coins, u => u.Coins + grant.Coins).SetProperty(u => u.Xp, u => u.Xp + grant.Xp), ct);
        if (grant.Xp <= 0) return true;

        foreach (var levelUp in Rewards.LevelUps(userId, oldXp, oldXp + grant.Xp)) await GrantAsync(userId, levelUp, ct);
        var week = Clock.WeekStart(now);
        var weekStart = Clock.StartOf(week);
        if (await db.RewardEvents.Where(e => e.UserId == userId && e.CreatedAt >= weekStart).SumAsync(e => e.Xp, ct) >= GameRules.WeeklyXpGoal)
            await GrantAsync(userId, new Grant(RewardType.WeeklyXpGoal, GameRules.CoinsWeeklyXpGoal, 0, 0, null, $"weekly_xp_goal:{week:yyyy-MM-dd}:{userId}"), ct);
        return true;
    }

    // A purchase: coins leave only when there are enough (one atomic update), with a negative ledger row.
    public async Task<bool> SpendAsync(Guid userId, RewardType type, int coins, Guid reference, string dedupeKey, CancellationToken ct)
    {
        if (await db.Users.Where(u => u.Id == userId && u.Coins >= coins).ExecuteUpdateAsync(s => s.SetProperty(u => u.Coins, u => u.Coins - coins), ct) == 0)
            return false;
        await db.Database.ExecuteSqlAsync(
            $"""
            insert into reward_events (id, user_id, type, coins, xp, sp, reference_id, dedupe_key, created_at)
            values ({Guid.CreateVersion7()}, {userId}, {Rewards.Name(type)}, {-coins}, 0, 0, {reference}, {dedupeKey}, {time.GetUtcNow()})
            """, ct);
        return true;
    }

    // SP already earned today from these sources, for the daily caps (100 from wins, 10 from learning).
    public Task<int> SpTodayAsync(Guid userId, RewardType[] types, CancellationToken ct)
    {
        var since = Clock.StartOf(Clock.Today(time.GetUtcNow()));
        return db.RewardEvents.Where(e => e.UserId == userId && types.Contains(e.Type) && e.CreatedAt >= since).SumAsync(e => e.Sp, ct);
    }

    // The first activity of a day moves the streak (the caller saves the tracked user); 7, 30 and 100 days pay once per streak.
    public async Task RecordActivityAsync(User user, CancellationToken ct)
    {
        var today = Clock.Today(time.GetUtcNow());
        if (await db.Database.ExecuteSqlAsync($"insert into activity_days (user_id, day) values ({user.Id}, {today}) on conflict do nothing", ct) == 0)
            return;
        if (Streaks.Apply(user, today) is not { } days) return;
        var startedOn = today.AddDays(1 - user.StreakCount);
        var coins = GameRules.StreakMilestones.First(m => m.Days == days).Coins;
        await GrantAsync(user.Id, new Grant(RewardType.StreakMilestone, coins, 0, 0, null, $"streak_milestone:{user.Id}:{startedOn:yyyy-MM-dd}:{days}"), ct);
    }
}
