using App.Application.Content;
using App.Application.Me;

namespace App.Application.Queue;

// Mirrors docs/tehnicki-dokument.md §6.6. Keep frontend/src/types/api.ts in sync.

public enum QueuePhase { Idle, Waiting, Proposed, Matched }

public record QueueProposalDto(Guid Id, UserRefDto Opponent, DateTimeOffset ExpiresAt, bool IAccepted);

public record GhostOfferDto(UserRefDto Opponent, DateTimeOffset RecordedAt, TopicDto Topic);

// Polled about once a second while queued. matchId comes with status "matched": navigate to the match.
public record QueueStateDto(
    QueuePhase Status, DateTimeOffset? EnteredAt, DateTimeOffset ServerTime, int? RangeNow, Guid[] TopicIds,
    QueueProposalDto? Proposal, GhostOfferDto? GhostOffer, bool BotOffer, Guid? MatchId);
