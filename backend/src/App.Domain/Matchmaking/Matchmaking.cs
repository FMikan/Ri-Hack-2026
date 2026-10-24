namespace App.Domain.Matchmaking;

public enum QueueStatus { Waiting, Proposed }

public enum ProposalStatus { Pending, Matched, Expired, Declined }

// A player waiting for a ranked opponent (docs §3.5). Rating and topics are frozen when they join.
public class QueueEntry
{
    public required Guid UserId { get; init; }
    public required int Rating { get; init; }
    public required List<Guid> TopicIds { get; init; }
    public required DateTimeOffset EnteredAt { get; init; } // kept when a proposal falls through
    public QueueStatus Status { get; set; } = QueueStatus.Waiting;
    public Guid? ProposalId { get; set; }
}

// "Opponent found": both must accept within 10 s.
public class MatchProposal
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserA { get; init; }
    public required Guid UserB { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? AAcceptedAt { get; set; }
    public DateTimeOffset? BAcceptedAt { get; set; }
    public ProposalStatus Status { get; set; } = ProposalStatus.Pending;
    public Guid? MatchId { get; set; }
}

public record QueueCandidate(Guid UserId, int Rating, IReadOnlyCollection<Guid> TopicIds, DateTimeOffset EnteredAt);

public static class MatchmakingRules
{
    // ±100 at first, +50 every 10 s, at most ±400 (idea §4).
    public static int Range(TimeSpan waited) =>
        Math.Min(GameRules.MaxRatingGap, GameRules.QueueInitialRange + GameRules.QueueRangeStep * (int)(waited.TotalSeconds / GameRules.QueueStepSeconds));

    // In my range and sharing a topic; closest rating first, then more shared topics, then the longer wait.
    // Not a third ranked match in a row against the same player (a rematch is the exception, ghosts do not count).
    public static QueueCandidate? Pick(QueueCandidate me, IEnumerable<QueueCandidate> others, IReadOnlyList<Guid> myLastTwoRankedOpponents, DateTimeOffset now)
    {
        var range = Range(now - me.EnteredAt);
        return others
            .Where(o => o.UserId != me.UserId && Math.Abs(o.Rating - me.Rating) <= range && o.TopicIds.Intersect(me.TopicIds).Any())
            .Where(o => myLastTwoRankedOpponents.Count < 2 || myLastTwoRankedOpponents.Any(id => id != o.UserId))
            .OrderBy(o => Math.Abs(o.Rating - me.Rating))
            .ThenByDescending(o => o.TopicIds.Intersect(me.TopicIds).Count())
            .ThenBy(o => o.EnteredAt)
            .FirstOrDefault();
    }
}
