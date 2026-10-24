using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using App.Application.Content;
using App.Application.Me;
using App.Domain.Content;
using App.Domain.Matches;
using App.Domain.Progress;

namespace App.Application.Matches;

// Mirrors docs/tehnicki-dokument.md §6.5. Keep frontend/src/types/api.ts in sync.

public record StartBotMatchRequest
{
    [Required]
    public required BotPersona Persona { get; init; }

    [Required]
    public required Guid TopicId { get; init; }
}

public record StartPracticeRequest
{
    [Required]
    public required Guid TopicId { get; init; }

    public bool Timed { get; init; } = true;

    [MaxLength(10)]
    public Guid[]? ConceptIds { get; init; } // "Practice this", "Due for review", suggestions: these concepts come first
}

public record SubmitAnswerRequest
{
    [Range(0, 9)]
    public required int QuestionIndex { get; init; }

    public JsonElement Payload { get; init; } // shape per question type (AnswerPayload in api.ts); anything else simply scores 0

    [Range(0, 600_000)]
    public required int ClientElapsedMs { get; init; } // measured from the first render of the question
}

public enum MatchStatus { Veto, Lobby, InProgress, Finished, Aborted }

public enum MatchOutcome { Win, Loss, Draw, Aborted, Completed }

public record AnswerResultDto(JsonNode? Payload, bool IsCorrect, double Fraction, int Points, int ElapsedMs, bool TimedOut, AnswerLabel Label);

public record MyAnswerDto(JsonNode? Payload, int ElapsedMs);

public record QuestionTurnDto(int Index, int Total, QuestionPublicDto Question, DateTimeOffset StartedAt, DateTimeOffset? DeliveredAt, MyAnswerDto? MyAnswer);

public record InterstitialDto(int Index, AnswerKeyDto Key, AnswerResultDto Mine, AnswerResultDto? Opponent);

public record PlayerDto(int Seat, SeatKind Kind, UserRefDto? User, BotPersona? Persona, DateTimeOffset? GhostRecordedAt, int DisplayRating, int Score, bool Connected, bool HasAnswered);

public record ReviewQuestionDto(int Index, QuestionPublicDto Question, AnswerKeyDto Key, ConceptDto Concept, MatchSlot Slot, AnswerResultDto Mine, AnswerResultDto? Opponent, ExplanationDto? Explanation, bool Reported);

public record PracticeConceptCardDto(ConceptDto Concept, double MasteryDelta);

public record ResultCardsDto(ReviewQuestionDto? Blunder, ReviewQuestionDto? Brilliant, PracticeConceptCardDto? PracticeConcept);

public record MyResultDto(int Score, int Correct, int? RatingBefore, int? RatingAfter, int Sp, int Coins, int Xp, bool StreakSecured);

public record OpponentResultDto(int Score, int Correct);

public record MatchResultDto(
    int? WinnerSeat, MatchResultReason Reason, MyResultDto Mine, OpponentResultDto? Opponent, ResultCardsDto Cards,
    bool LessonAvailable, bool LessonRewardEligible, Guid? LessonId, bool RematchAvailable, bool ReviewAvailable);

// question: in question and interstitial phases; interstitial: only in interstitial; result: only when finished or aborted.
public record MatchStateDto(
    Guid Id, MatchKind Kind, MatchStatus Status, MatchPhase Phase, DateTimeOffset ServerTime, DateTimeOffset? PhaseDeadline,
    TopicDto? Topic, bool Timed, int MySeat, PlayerDto[] Players, QuestionTurnDto? Question, InterstitialDto? Interstitial, MatchResultDto? Result);

public record OpponentDto(SeatKind Kind, BotPersona? Persona, UserRefDto? User, int DisplayRating);

public record MatchSummaryDto(
    Guid Id, MatchKind Kind, TopicDto? Topic, OpponentDto? Opponent, int MyScore, int OpponentScore, MatchOutcome Outcome,
    int? RatingDelta, DateTimeOffset? FinishedAt, Guid? LessonId);

public record BotDto(BotPersona Persona, int DisplayRating, bool Recommended);

public record StartCalibrationRequest
{
    [Required]
    public required Guid TopicId { get; init; }
}

public record MasteryDeltaDto(ConceptDto Concept, double Before, double After);

// The detailed analysis (F2): every question with both answers, the key and the cached explanation, and how mastery moved.
public record MatchReviewDto(MatchSummaryDto Match, ReviewQuestionDto[] Questions, MasteryDeltaDto[] MasteryDelta);
