using System.Net.Http.Json;
using App.Application.Economy;
using App.Application.Lessons;
using App.Application.Matches;
using App.Application.Me;
using App.Domain.Economy;
using App.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace App.Api.Tests;

// F4a: the ledger and everything it pays: matches, lessons, level-ups, the weekly XP goal and the streak.
public class EconomyTests(TestApp app) : RankedTestBase(app)
{
    private Task<User> UserAsync(Session session) => App.InDbAsync(db => db.Users.FirstAsync(u => u.Id == session.Auth.User.Id));

    private Task<List<RewardEvent>> LedgerAsync(Session session) =>
        App.InDbAsync(db => db.RewardEvents.Where(e => e.UserId == session.Auth.User.Id).OrderBy(e => e.CreatedAt).ToListAsync());

    // The invariant behind every balance: the cached coins and XP are the sums of the ledger.
    private async Task AssertLedgerMatchesAsync(Session session)
    {
        var (user, ledger) = (await UserAsync(session), await LedgerAsync(session));
        Assert.Equal((ledger.Sum(e => e.Coins), ledger.Sum(e => e.Xp)), (user.Coins, user.Xp));
    }

    private async Task<T> WithRewarderAsync<T>(Func<Rewarder, IServiceProvider, Task<T>> action)
    {
        using var scope = App.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<Rewarder>(), scope.ServiceProvider);
    }

    private Task GrantAsync(Session session, Grant grant) => WithRewarderAsync((r, _) => r.GrantAsync(session.Auth.User.Id, grant, default));

    [Fact]
    public async Task Ranked_match_pays_both_players_and_the_ledger_matches_the_balance()
    {
        var topics = await PlayableTopicsAsync(3);
        var ana = await PlayerAsync("ana", topics);
        var bob = await PlayerAsync("bob", topics);
        var match = await RankedAsync(ana, bob);

        var (a, b) = await PlayBothAsync(ana, bob, await ThroughVetoAsync(ana, bob, match), right: (seat, _) => seat == 0);

        Assert.Equal((15, 10, 100), (a.Result!.Mine.Coins, a.Result.Mine.Sp, a.Result.Mine.Xp)); // 50 for the match + 5 per answer
        Assert.Equal((5, 0, 100), (b.Result!.Mine.Coins, b.Result.Mine.Sp, b.Result.Mine.Xp));
        Assert.True(a.Result.Mine.StreakSecured);
        var me = (await ana.Client.GetFromJsonAsync<MeDto>("/api/me", App.Json))!;
        Assert.Equal((15, 100, 10, 1), (me.Coins, me.Xp, me.SeasonPoints, me.Streak.Count));
        await AssertLedgerMatchesAsync(ana);
        await AssertLedgerMatchesAsync(bob);
        var rewards = (await ana.Client.GetFromJsonAsync<RewardEventDto[]>("/api/me/rewards", App.Json))!;
        Assert.Equal(new[] { RewardType.MatchWin, RewardType.AnswerXp, RewardType.MatchXp }.Order(), rewards.Select(r => r.Type).Order());
        Assert.All(rewards, r => Assert.Equal(match.Id, r.ReferenceId));
    }

    [Fact]
    public async Task Upset_bonus_and_the_daily_win_sp_cap()
    {
        var topics = await PlayableTopicsAsync(3);
        var ana = await PlayerAsync("ana", topics, rating: 1000);
        var bob = await PlayerAsync("bob", topics, rating: 1100);
        await GrantAsync(ana, new Grant(RewardType.MatchWin, 0, 0, 88, null, "earlier-wins-today"));
        var match = await RankedAsync(ana, bob);

        var (a, _) = await PlayBothAsync(ana, bob, await ThroughVetoAsync(ana, bob, match), right: (seat, _) => seat == 0);

        Assert.Equal(12, a.Result!.Mine.Sp); // 10 for the win + 2 of the 5 upset SP: the day's cap is 100
        Assert.Contains(await LedgerAsync(ana), e => e is { Type: RewardType.RatingUpset, Sp: 2 });
    }

    [Fact]
    public async Task Bot_match_grants_xp_but_not_coins_or_sp_and_counts_for_the_streak()
    {
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");

        var final = await FinishedBotMatchAsync(ana, topic.Id);

        Assert.Equal((0, 0, 100), (final.Result!.Mine.Coins, final.Result.Mine.Sp, final.Result.Mine.Xp));
        var me = (await ana.Client.GetFromJsonAsync<MeDto>("/api/me", App.Json))!;
        Assert.Equal((0, 100, 1, true), (me.Coins, me.Xp, me.Streak.Count, me.Streak.SecuredToday));
        Assert.Equal(1, await App.InDbAsync(db => db.ActivityDays.CountAsync(d => d.UserId == ana.Auth.User.Id)));
    }

    [Fact]
    public async Task Dedupe_key_prevents_double_reward()
    {
        var ana = await RegisterAsync("ana");
        var grant = new Grant(RewardType.MatchWin, 15, 0, 10, null, "match_win:same-match:ana");

        var first = await WithRewarderAsync((r, _) => r.GrantAsync(ana.Auth.User.Id, grant, default));
        var second = await WithRewarderAsync((r, _) => r.GrantAsync(ana.Auth.User.Id, grant, default));

        Assert.Equal(grant, first);
        Assert.Null(second);
        Assert.Single(await LedgerAsync(ana));
        Assert.Equal(15, (await UserAsync(ana)).Coins);
    }

    [Fact]
    public async Task Level_up_rewards_50_times_level()
    {
        var ana = await RegisterAsync("ana");

        await GrantAsync(ana, new Grant(RewardType.MatchXp, 0, 520, 0, null, "xp-1"));

        var user = await UserAsync(ana);
        Assert.Equal(3, Level.Of(user.Xp));
        Assert.Equal(100 + 150, (await LedgerAsync(ana)).Where(e => e.Type == RewardType.LevelUp).Sum(e => e.Coins));
        await AssertLedgerMatchesAsync(ana);
    }

    [Fact]
    public async Task Weekly_xp_goal_500_gives_100_coins_once_per_week()
    {
        var ana = await RegisterAsync("ana");

        await GrantAsync(ana, new Grant(RewardType.MatchXp, 0, 300, 0, null, "xp-1"));
        await GrantAsync(ana, new Grant(RewardType.MatchXp, 0, 250, 0, null, "xp-2"));
        await GrantAsync(ana, new Grant(RewardType.MatchXp, 0, 100, 0, null, "xp-3"));
        App.Clock.Advance(TimeSpan.FromDays(7));
        await GrantAsync(ana, new Grant(RewardType.MatchXp, 0, 600, 0, null, "xp-4"));

        Assert.Equal(2, (await LedgerAsync(ana)).Count(e => e is { Type: RewardType.WeeklyXpGoal, Coins: 100 }));
        await AssertLedgerMatchesAsync(ana);
    }

    [Fact]
    public async Task Streak_milestone_7_days_gives_100_coins_once()
    {
        var ana = await RegisterAsync("ana");

        for (var day = 0; day < 8; day++)
        {
            await WithRewarderAsync(async (r, sp) =>
            {
                var db = sp.GetRequiredService<App.Infrastructure.AppDbContext>();
                var user = await db.Users.FirstAsync(u => u.Id == ana.Auth.User.Id);
                await r.RecordActivityAsync(user, default);
                await r.RecordActivityAsync(user, default); // twice a day counts once
                return await db.SaveChangesAsync();
            });
            App.Clock.Advance(TimeSpan.FromDays(1));
        }

        var user = await UserAsync(ana);
        Assert.Equal(8, user.StreakCount);
        Assert.Equal(100, Assert.Single(await LedgerAsync(ana), e => e.Type == RewardType.StreakMilestone).Coins);
    }

    [Fact]
    public async Task Freeze_is_consumed_on_a_missed_day()
    {
        var ana = await RegisterAsync("ana");
        await App.InDbAsync(db => db.Users.Where(u => u.Id == ana.Auth.User.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.StreakFreezes, 1)));

        foreach (var advance in new[] { 0, 2 })
        {
            App.Clock.Advance(TimeSpan.FromDays(advance));
            await WithRewarderAsync(async (r, sp) =>
            {
                var db = sp.GetRequiredService<App.Infrastructure.AppDbContext>();
                await r.RecordActivityAsync(await db.Users.FirstAsync(u => u.Id == ana.Auth.User.Id), default);
                return await db.SaveChangesAsync();
            });
        }

        var user = await UserAsync(ana);
        Assert.Equal((2, 0), (user.StreakCount, user.StreakFreezes));
    }

    [Theory]
    [InlineData(6, 0)]
    [InlineData(8, 50)]
    public async Task Same_concept_rewards_at_most_once_in_7_days(int daysSincePatched, int coins)
    {
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");
        var match = await FinishedBotMatchAsync(ana, topic.Id);
        var lesson = (await ReadAsync<LessonDto>(await PostAsync(ana.Client, "/api/lessons", new StartLessonRequest { MatchId = match.Id })))!;
        var concepts = lesson.Concepts.Select(c => c.Id).ToList();
        await App.InDbAsync(db => db.ConceptMastery.Where(m => m.UserId == ana.Auth.User.Id && concepts.Contains(m.ConceptId))
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.PatchedAt, App.Clock.GetUtcNow().AddDays(-daysSincePatched))));

        var result = (await ReadAsync<LessonResultDto>(await PostAsync(ana.Client, $"/api/lessons/{lesson.Id}/check", new
        {
            answers = lesson.Check.Select(c => new { questionId = c.Question.Id, payload = QuestionFixtures.RightAnswer(c.Question.Type) }),
        })))!;

        Assert.True(result.Passed);
        Assert.Equal(coins, result.Rewards.Coins);
        Assert.Equal(coins == 0 ? 0 : 2 * concepts.Count, result.Rewards.Sp);
        Assert.Equal(coins == 0 ? 0 : 100, result.Rewards.Xp);
        await AssertLedgerMatchesAsync(ana);
    }
}
