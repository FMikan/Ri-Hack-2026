using System.Text.Json;
using App.Application.Content;
using App.Application.Social;
using App.Domain;
using App.Domain.Content;
using App.Domain.Economy;
using App.Domain.Lessons;
using App.Domain.Matches;
using App.Domain.Progress;
using App.Domain.Seasons;
using App.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace App.Application.Me;

// Statistics, achievements, titles and the public profile (idea §2, §14), all derived from the player's history.
// Answers to retired questions (reverted_at) no longer count.
public class ProfileService(IAppDbContext db, MeService me, ContentService content, TimeProvider time)
{
    public async Task<StatsDto> GetStatsAsync(Guid userId, CancellationToken ct)
    {
        var results = await db.MatchPlayers.Where(p => p.UserId == userId && p.Kind == SeatKind.Human)
            .Join(db.Matches.Where(m => m.Phase == MatchPhase.Done && m.ResultReason != MatchResultReason.Aborted
                    && m.Kind != MatchKind.Practice && m.Kind != MatchKind.Calibration),
                p => p.MatchId, m => m.Id, (p, m) => new { p.Seat, m.WinnerSeat })
            .ToListAsync(ct);
        var wins = results.Count(r => r.WinnerSeat == r.Seat);
        var draws = results.Count(r => r.WinnerSeat is null);

        var byType = await db.Answers.Where(a => a.UserId == userId && a.RevertedAt == null)
            .GroupBy(a => a.QuestionType)
            .Select(g => new
            {
                Type = g.Key,
                Accuracy = g.Average(a => a.IsCorrect ? 100.0 : 0.0),
                AvgMs = g.Where(a => a.TimeLimitMs != null).Average(a => (double?)a.ElapsedMs),
                Blunders = g.Count(a => a.Label == AnswerLabel.Blunder),
                Brilliants = g.Count(a => a.Label == AnswerLabel.Brilliant),
            })
            .ToListAsync(ct);

        return new StatsDto(
            results.Count, wins, results.Count - wins - draws, draws, results.Count == 0 ? 0 : Math.Round(100.0 * wins / results.Count, 1),
            byType.ToDictionary(t => Name(t.Type), t => Math.Round(t.Accuracy, 1)),
            byType.Where(t => t.AvgMs is not null).ToDictionary(t => Name(t.Type), t => (int)Math.Round(t.AvgMs!.Value)),
            byType.Where(t => t.Blunders > 0).MaxBy(t => t.Blunders)?.Type,
            byType.Sum(t => t.Brilliants),
            await ConceptsPatchedAsync(userId, ct),
            await db.ConceptMastery.CountAsync(m => m.UserId == userId && m.IsGraduated, ct),
            await me.RatingHistoryAsync(userId, ct));
    }

    // 403 for a friends-only profile unless the viewer is a friend (or the owner).
    public async Task<PublicProfileDto> GetPublicAsync(Guid viewerId, string username, CancellationToken ct)
    {
        var normalized = User.Normalize(username);
        var user = await db.Users.Include(u => u.Topics).FirstOrDefaultAsync(u => u.UsernameNormalized == normalized, ct) ?? throw SocialService.UserNotFound();
        if (user.ProfileVisibility == ProfileVisibility.Friends && user.Id != viewerId && !await SocialService.FriendIds(db, user.Id).ContainsAsync(viewerId, ct))
            throw new AppException(ErrorCodes.ProfilePrivate, "This profile is visible to friends only.", 403);

        var progress = await me.TopicProgressAsync(user.Id, ct);
        var active = user.Topics.Select(t => t.TopicId).ToHashSet();
        var stats = await GetStatsAsync(user.Id, ct);
        var titles = await db.UserTitles.Where(t => t.UserId == user.Id && t.Active)
            .Join(db.Titles, ut => ut.TitleId, t => t.Id, (ut, t) => new { t.Name, ut.EarnedAt })
            .OrderByDescending(t => t.EarnedAt).Select(t => t.Name).ToArrayAsync(ct);
        var (_, earned) = await EarnAsync(user, ct);
        return new PublicProfileDto(
            UserRefDto.From(user),
            (await content.ListTopicsAsync(ct))
                .Where(t => active.Contains(t.Id) || progress.ContainsKey(t.Id))
                .Select(t =>
                {
                    var p = progress.GetValueOrDefault(t.Id);
                    return new PublicTopicDto(t, p?.Mastery, MeService.Completion(p?.Graduated ?? 0, t.ConceptCount));
                })
                .ToArray(),
            titles,
            [.. earned.OrderBy(e => e.Value).Select(e => e.Key)],
            new PublicStatsDto(stats.Matches, stats.WinRate, stats.ConceptsGraduated));
    }

    // Every achievement with its progress; the newly reached ones are recorded now.
    public async Task<AchievementDto[]> GetAchievementsAsync(Guid userId, CancellationToken ct)
    {
        var (snapshot, earned) = await EarnAsync(await db.Users.Include(u => u.Topics).FirstAsync(u => u.Id == userId, ct), ct);
        return AchievementRules.All.Select(rule =>
        {
            if (earned.TryGetValue(rule.Key, out var at)) return new AchievementDto(rule.Key, rule.Name, at, null, null);
            var (progress, target) = rule.Measure(snapshot);
            return new AchievementDto(rule.Key, rule.Name, null, Math.Min(progress, target), target);
        }).ToArray();
    }

    // Earned and lost-again knowledge titles alike, newest first.
    public async Task<TitleDto[]> GetTitlesAsync(Guid userId, CancellationToken ct) =>
        (await db.UserTitles.Where(t => t.UserId == userId)
            .Join(db.Titles, ut => ut.TitleId, t => t.Id, (ut, t) => new { Title = t, ut.Count, ut.Active, ut.EarnedAt })
            .OrderByDescending(x => x.EarnedAt)
            .ToListAsync(ct))
        .Select(x => new TitleDto(x.Title.Id, x.Title.Key, x.Title.Name, x.Title.Kind, x.Count, x.Active)).ToArray();

    // Achievements are measured on the player's history whenever it is read (their list, their public profile), and a
    // reached one is recorded once (earnedAt = when it was first noticed).
    private async Task<(UserSnapshot Snapshot, Dictionary<string, DateTimeOffset> Earned)> EarnAsync(User user, CancellationToken ct)
    {
        var snapshot = await SnapshotAsync(user, ct);
        var earned = await db.UserAchievements.Where(a => a.UserId == user.Id).ToDictionaryAsync(a => a.Key, a => a.EarnedAt, ct);
        var now = time.GetUtcNow();
        foreach (var key in AchievementRules.Evaluate(snapshot, earned.Keys.ToHashSet()))
        {
            await db.Database.ExecuteSqlAsync($"insert into user_achievements (user_id, key, earned_at) values ({user.Id}, {key}, {now}) on conflict do nothing", ct);
            earned[key] = now;
        }
        return (snapshot, earned);
    }

    private async Task<UserSnapshot> SnapshotAsync(User user, CancellationToken ct)
    {
        var finished = db.MatchPlayers.Where(p => p.UserId == user.Id && p.Kind == SeatKind.Human)
            .Join(db.Matches.Where(m => m.Phase == MatchPhase.Done), p => p.MatchId, m => m.Id, (p, m) => new { p.Seat, p.DisplayRating, m.Id, m.Kind, m.WinnerSeat })
            .Where(x => x.WinnerSeat == x.Seat);
        var wins = await finished.CountAsync(ct);
        var upsets = await finished.CountAsync(x => (x.Kind == MatchKind.Ranked || x.Kind == MatchKind.Ghost)
            && db.MatchPlayers.Any(o => o.MatchId == x.Id && o.Seat != x.Seat && o.DisplayRating - x.DisplayRating >= AchievementRules.GiantSlayerGap), ct);
        var brilliants = await db.Answers.CountAsync(a => a.UserId == user.Id && a.RevertedAt == null && a.Label == AnswerLabel.Brilliant, ct);

        // The best streak: today's, or the highest milestone the ledger ever paid (a streak may have broken since).
        var milestones = await db.RewardEvents.Where(e => e.UserId == user.Id && e.Type == RewardType.StreakMilestone).Select(e => e.Coins).ToListAsync(ct);
        var bestStreak = GameRules.StreakMilestones.Where(m => milestones.Contains(m.Coins)).Select(m => m.Days)
            .Append(Streaks.Current(user, Clock.Today(time.GetUtcNow()))).Max();

        var progress = await me.TopicProgressAsync(user.Id, ct);
        var active = user.Topics.Select(t => t.TopicId).ToList();
        var above = active.Count(t => progress.GetValueOrDefault(t)?.Mastery > AchievementRules.TopicMastery);

        var days = await db.DailyChallengeAttempts.Where(a => a.UserId == user.Id && a.AnsweredAt != null).OrderBy(a => a.Day).Select(a => a.Day).ToListAsync(ct);
        int run = 0, best = 0;
        for (var i = 0; i < days.Count; i++)
        {
            run = i > 0 && days[i].DayNumber == days[i - 1].DayNumber + 1 ? run + 1 : 1;
            best = Math.Max(best, run);
        }

        return new UserSnapshot(wins, brilliants, bestStreak, await ConceptsPatchedAsync(user.Id, ct), upsets, above, active.Count, best);
    }

    // Distinct concepts patched by passed lessons.
    private async Task<int> ConceptsPatchedAsync(Guid userId, CancellationToken ct) =>
        (await db.Lessons.Where(l => l.UserId == userId && l.Status == LessonStatus.Passed).Select(l => l.ConceptIds).ToListAsync(ct))
        .SelectMany(c => c).Distinct().Count();

    private static string Name(QuestionType type) => JsonNamingPolicy.SnakeCaseLower.ConvertName(type.ToString());
}
