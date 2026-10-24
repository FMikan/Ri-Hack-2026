using App.Application.Content;
using App.Application.Leaderboards;
using App.Domain;
using App.Domain.Seasons;
using Microsoft.EntityFrameworkCore;

namespace App.Application.Seasons;

// The minute tick (docs §8). Every step is idempotent, so a late or repeated run is harmless.
public class HousekeepingService(IAppDbContext db, LeaderboardService leaderboards, TimeProvider time)
{
    public async Task RunAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        // Unjoined challenges are kept a day after they expire, so a late guest reads "expired" rather than "not found".
        await db.FriendChallenges.Where(c => c.MatchId == null && c.ExpiresAt < now.AddDays(-1)).ExecuteDeleteAsync(ct);
        await db.RefreshTokens.Where(t => t.ExpiresAt < now).ExecuteDeleteAsync(ct);
        await RefreshGrandmastersAsync(ct);
        await CloseWeekAsync(now, ct);
        await CloseSeasonsAsync(now, ct);
        await RefreshKnowledgeTitlesAsync(now, ct);
    }

    // Once per local day (the tick calls it when the day changes).
    public Task DailyAsync(CancellationToken ct) => db.RecalibrateDifficultyAsync(ct);

    // The global top 100 by Elo among players with 10+ ranked matches (idea §14); only changed rows are written.
    private Task RefreshGrandmastersAsync(CancellationToken ct)
    {
        var top = db.Users.Where(u => u.RankedMatches >= GameRules.ProvisionalMatches)
            .OrderByDescending(u => u.Rating).ThenBy(u => u.UsernameNormalized)
            .Take(GameRules.GrandmasterTop)
            .Select(u => u.Id);
        return db.Users.Where(u => u.IsGrandmaster != top.Contains(u.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsGrandmaster, u => !u.IsGrandmaster), ct);
    }

    // After Monday 00:00 (idea §13): last week's board as it stood, and one more "Weekly Top 10" for its top 10.
    private async Task CloseWeekAsync(DateTimeOffset now, CancellationToken ct)
    {
        var week = Clock.WeekStart(now).AddDays(-7);
        if (await db.WeeklyResults.AnyAsync(r => r.WeekStart == week, ct)) return;
        var board = await leaderboards.WeekAsync(week, ct);
        if (board.Count == 0) return;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.WeeklyResults.AddRange(board.Select((r, i) => new WeeklyResult { WeekStart = week, UserId = r.UserId, Rank = i + 1, Points = r.Value }));
        await db.SaveChangesAsync(ct);
        foreach (var row in board.Take(GameRules.WeeklyTitleTop).Where(r => r.Value > 0))
            await db.GrantTitleAsync(row.UserId, SeasonTitles.WeeklyTop, TitleKind.Weekly, now, ct);
        await transaction.CommitAsync(ct);
    }

    // A season that ended (idea §13): final ranks of the players with SP, Top 5/50/100 and best-tier titles, the cosmetic
    // drop of the best tier. Elo stays; the next season's SP start at 0 (a new user_seasons row).
    private async Task CloseSeasonsAsync(DateTimeOffset now, CancellationToken ct)
    {
        foreach (var season in await db.Seasons.Where(s => s.EndsAt <= now && s.ClosedAt == null).OrderBy(s => s.Number).ToListAsync(ct))
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var rows = await db.UserSeasons.Where(s => s.SeasonId == season.Id).ToListAsync(ct);
            var board = await leaderboards.OrderedAsync(rows.Where(r => r.Sp > 0).Select(r => new LeaderboardRow(r.UserId, r.Sp)), ct);
            for (var i = 0; i < board.Count; i++) rows.First(r => r.UserId == board[i].UserId).FinalRank = i + 1;
            var drops = await db.ShopItems.Where(i => i.SeasonDropTier != null).ToListAsync(ct);

            foreach (var row in rows)
            {
                if (row.FinalRank is { } rank && SeasonTitles.ForRank(season.Number, rank) is { } top)
                    await db.GrantTitleAsync(row.UserId, top, TitleKind.Season, now, ct);
                await db.GrantTitleAsync(row.UserId, SeasonTitles.ForTier(season.Number, row.BestTier), TitleKind.Season, now, ct);
                foreach (var item in drops.Where(d => d.SeasonDropTier == row.BestTier.ToString().ToLowerInvariant()))
                    await db.Database.ExecuteSqlAsync(
                        $"insert into user_items (user_id, item_id, acquired_at) values ({row.UserId}, {item.Id}, {now}) on conflict do nothing", ct);
            }
            season.ClosedAt = now;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
    }

    // "<Topic> master" while every concept of the topic is graduated (idea §14). A graduated concept that drops below 70
    // loses its graduation (Mastery.Apply), and with it the title, which also comes off if it was equipped.
    // ponytail: rescans every graduated (user, topic) group each minute; cheap while graduation is rare (55+ days a concept).
    private async Task RefreshKnowledgeTitlesAsync(DateTimeOffset now, CancellationToken ct)
    {
        var topics = await db.Topics.Select(t => new { t.Id, t.Slug, t.Name, Concepts = t.Concepts.Count }).ToDictionaryAsync(t => t.Id, ct);
        var graduated = await db.ConceptMastery.Where(m => m.IsGraduated)
            .GroupBy(m => new { m.UserId, m.TopicId })
            .Select(g => new { g.Key.UserId, g.Key.TopicId, Count = g.Count() })
            .ToListAsync(ct);
        var masters = graduated.Where(g => g.Count >= topics[g.TopicId].Concepts)
            .Select(g => (g.UserId, Title: SeasonTitles.Master(topics[g.TopicId].Slug, topics[g.TopicId].Name)))
            .ToList();
        foreach (var (userId, title) in masters) await db.GrantTitleAsync(userId, title, TitleKind.Knowledge, now, ct);

        var keep = masters.Select(m => (m.UserId, m.Title.Key)).ToHashSet();
        var held = await db.UserTitles.Where(t => t.Active)
            .Join(db.Titles.Where(t => t.Kind == TitleKind.Knowledge), ut => ut.TitleId, t => t.Id, (ut, t) => new { ut.UserId, ut.TitleId, t.Key })
            .ToListAsync(ct);
        foreach (var lost in held.Where(h => !keep.Contains((h.UserId, h.Key))))
        {
            await db.UserTitles.Where(t => t.UserId == lost.UserId && t.TitleId == lost.TitleId).ExecuteUpdateAsync(s => s.SetProperty(t => t.Active, false), ct);
            await db.Users.Where(u => u.Id == lost.UserId && u.EquippedTitleId == lost.TitleId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.EquippedTitleId, (Guid?)null), ct);
        }
    }
}
