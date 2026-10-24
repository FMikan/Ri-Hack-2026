using App.Application.Me;
using App.Application.Seasons;
using App.Application.Social;
using App.Domain;
using App.Domain.Matches;
using Microsoft.EntityFrameworkCore;

namespace App.Application.Leaderboards;

// Mirrors docs/tehnicki-dokument.md §6.9. Keep frontend/src/types/api.ts in sync.

public enum Board { Global, Season, Weekly, Topic, Friends }

// value: Elo (global, friends), season points, this week's ranked points, or graduated concepts of the topic.
public record LeaderboardEntryDto(int Rank, UserRefDto User, int Value);

// around: my row with up to two players above and below.
public record LeaderboardMeDto(LeaderboardEntryDto Entry, LeaderboardEntryDto[] Around);

public record LeaderboardDto(Board Board, LeaderboardEntryDto[] Entries, LeaderboardMeDto? Me, DateTimeOffset? ResetsAt);

public record LeaderboardRow(Guid UserId, int Value);

// doubleLearningSp: the first two weeks, when patched concepts pay double SP.
public record SeasonDto(Guid Id, int Number, DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool DoubleLearningSp);

// Five boards (idea §14), each the top 100 plus my own position. Ties keep a stable order (username).
public class LeaderboardService(IAppDbContext db, TimeProvider time)
{
    public async Task<LeaderboardDto> GetAsync(Guid userId, Board board, string? topicSlug, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        (List<LeaderboardRow> Rows, DateTimeOffset? ResetsAt) result = board switch
        {
            Board.Global => (await GlobalAsync(ct), null),
            Board.Season => await SeasonAsync(now, ct),
            Board.Weekly => (await WeekAsync(Clock.WeekStart(now), ct), Clock.StartOf(Clock.WeekStart(now).AddDays(7))),
            Board.Topic => (await TopicAsync(topicSlug, ct), null),
            Board.Friends => (await FriendsAsync(userId, ct), null),
            _ => throw new AppException(ErrorCodes.LeaderboardNotFound, "This leaderboard does not exist.", 404),
        };
        var (rows, resetsAt) = result;

        // ponytail: ranks the whole board in memory; move the ranking into SQL (row_number) when a board holds tens of thousands.
        var me = rows.FindIndex(r => r.UserId == userId);
        var around = me < 0 ? [] : Enumerable.Range(me - GameRules.LeaderboardAround, 2 * GameRules.LeaderboardAround + 1).Where(i => i >= 0 && i < rows.Count).ToList();
        var shown = Enumerable.Range(0, Math.Min(GameRules.LeaderboardSize, rows.Count)).ToList();
        var ids = shown.Concat(around).Select(i => rows[i].UserId).Distinct().ToList();
        var users = await db.Users.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);
        LeaderboardEntryDto Entry(int i) => new(i + 1, UserRefDto.From(users[rows[i].UserId]), rows[i].Value);

        return new LeaderboardDto(board, shown.Select(Entry).ToArray(),
            me < 0 ? null : new LeaderboardMeDto(Entry(me), around.Select(Entry).ToArray()), resetsAt);
    }

    public async Task<SeasonDto> CurrentSeasonAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var season = await db.CurrentSeasonAsync(now, ct);
        return new SeasonDto(season.Id, season.Number, season.StartsAt, season.EndsAt, season.DoubleLearningSp(now));
    }

    // Points scored in ranked and ghost matches that finished in the week starting `week` (idea §13), best first.
    // Also the Monday snapshot (Housekeeping).
    public async Task<List<LeaderboardRow>> WeekAsync(DateOnly week, CancellationToken ct)
    {
        var (from, to) = (Clock.StartOf(week), Clock.StartOf(week.AddDays(7)));
        var points = await db.MatchPlayers.Where(p => p.Kind == SeatKind.Human && p.UserId != null)
            .Join(db.Matches.Where(m => (m.Kind == MatchKind.Ranked || m.Kind == MatchKind.Ghost) && m.FinishedAt >= from && m.FinishedAt < to
                    && m.ResultReason != MatchResultReason.Aborted),
                p => p.MatchId, m => m.Id, (p, _) => p)
            .GroupBy(p => p.UserId!.Value)
            .Select(g => new { UserId = g.Key, Points = g.Sum(p => p.Score) })
            .ToListAsync(ct);
        return await OrderedAsync(points.Select(p => new LeaderboardRow(p.UserId, p.Points)), ct);
    }

    // Season points of the season running now; resets when it ends.
    private async Task<(List<LeaderboardRow>, DateTimeOffset?)> SeasonAsync(DateTimeOffset now, CancellationToken ct)
    {
        var season = await db.CurrentSeasonAsync(now, ct);
        var rows = await db.UserSeasons.Where(s => s.SeasonId == season.Id && s.Sp > 0).Select(s => new { s.UserId, s.Sp }).ToListAsync(ct);
        return (await OrderedAsync(rows.Select(r => new LeaderboardRow(r.UserId, r.Sp)), ct), season.EndsAt);
    }

    // Elo, without provisional players (idea §14: 10+ ranked matches).
    private async Task<List<LeaderboardRow>> GlobalAsync(CancellationToken ct) =>
        (await db.Users.Where(u => u.RankedMatches >= GameRules.ProvisionalMatches)
            .OrderByDescending(u => u.Rating).ThenBy(u => u.UsernameNormalized)
            .Select(u => new { u.Id, u.Rating })
            .ToListAsync(ct))
        .Select(u => new LeaderboardRow(u.Id, u.Rating)).ToList();

    // Graduated concepts of the topic; topic mastery breaks ties. Everyone who answered something in it is on the board.
    private async Task<List<LeaderboardRow>> TopicAsync(string? slug, CancellationToken ct)
    {
        var topic = await db.Topics.FirstOrDefaultAsync(t => t.Slug == slug, ct)
            ?? throw new AppException(ErrorCodes.TopicNotFound, "The topic board needs ?topic=<slug> of an existing topic.", 404);
        return (await db.ConceptMastery.Where(m => m.TopicId == topic.Id && m.AnswerCount > 0)
                .GroupBy(m => m.UserId)
                .Select(g => new { UserId = g.Key, Graduated = g.Count(m => m.IsGraduated), Mastery = g.Average(m => m.Mastery) })
                .Join(db.Users, x => x.UserId, u => u.Id, (x, u) => new { x.UserId, x.Graduated, x.Mastery, u.UsernameNormalized })
                .OrderByDescending(x => x.Graduated).ThenByDescending(x => x.Mastery).ThenBy(x => x.UsernameNormalized)
                .ToListAsync(ct))
            .Select(x => new LeaderboardRow(x.UserId, x.Graduated)).ToList();
    }

    // Me and my accepted friends by Elo (provisional ratings included: it is a private board).
    private async Task<List<LeaderboardRow>> FriendsAsync(Guid userId, CancellationToken ct)
    {
        var friends = SocialService.FriendIds(db, userId);
        return (await db.Users.Where(u => u.Id == userId || friends.Contains(u.Id))
                .OrderByDescending(u => u.Rating).ThenBy(u => u.UsernameNormalized)
                .Select(u => new { u.Id, u.Rating })
                .ToListAsync(ct))
            .Select(u => new LeaderboardRow(u.Id, u.Rating)).ToList();
    }

    // Highest value first, ties by username.
    public async Task<List<LeaderboardRow>> OrderedAsync(IEnumerable<LeaderboardRow> rows, CancellationToken ct)
    {
        var list = rows.ToList();
        var ids = list.Select(r => r.UserId).ToList();
        var names = await db.Users.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.UsernameNormalized, ct);
        return [.. list.OrderByDescending(r => r.Value).ThenBy(r => names[r.UserId], StringComparer.Ordinal)];
    }
}
