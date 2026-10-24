using System.Text.Json;
using App.Application.Content;
using App.Application.Social;
using App.Domain;
using App.Domain.Content;
using App.Domain.Lessons;
using App.Domain.Matches;
using App.Domain.Progress;
using App.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace App.Application.Me;

// Statistics and the public profile (idea §2): everything is derived from answers, match_players and lessons.
public class ProfileService(IAppDbContext db, MeService me, ContentService content)
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

        var byType = await db.Answers.Where(a => a.UserId == userId)
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
            [],
            [],
            new PublicStatsDto(stats.Matches, stats.WinRate, stats.ConceptsGraduated));
    }

    // Distinct concepts patched by passed lessons.
    private async Task<int> ConceptsPatchedAsync(Guid userId, CancellationToken ct) =>
        (await db.Lessons.Where(l => l.UserId == userId && l.Status == LessonStatus.Passed).Select(l => l.ConceptIds).ToListAsync(ct))
        .SelectMany(c => c).Distinct().Count();

    private static string Name(QuestionType type) => JsonNamingPolicy.SnakeCaseLower.ConvertName(type.ToString());
}
