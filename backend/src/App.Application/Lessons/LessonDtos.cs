using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using App.Application.Content;
using App.Domain.Lessons;

namespace App.Application.Lessons;

// Mirrors docs/tehnicki-dokument.md §6.7. Keep frontend/src/types/api.ts in sync.

public record StartLessonRequest
{
    [Required]
    public required Guid MatchId { get; init; }
}

public record LessonCheckAnswer
{
    [Required]
    public required Guid QuestionId { get; init; }

    public JsonElement Payload { get; init; } // AnswerPayload of the question's type; anything else is wrong
}

public record SubmitLessonCheckRequest
{
    [Required]
    [MaxLength(3)]
    public required LessonCheckAnswer[] Answers { get; init; }
}

public record LessonCheckDto(QuestionPublicDto Question, bool IsVariantOfMissed);

public record LessonCheckResultDto(Guid QuestionId, bool IsCorrect, AnswerKeyDto Key, bool IsVariantOfMissed);

public record RewardsDto(int Coins, int Sp, int Xp);

public record LessonDto(
    Guid Id, Guid? MatchId, LessonStatus Status, ConceptDto[] Concepts, LessonCard[] Cards, LessonCheckDto[] Check,
    int Attempt, bool RewardEligible, DateTimeOffset? RetryAfter, LessonCheckResultDto[]? LastResults);

public record LessonResultDto(bool Passed, LessonCheckResultDto[] Results, ConceptDto[] PatchedConcepts, RewardsDto Rewards, DateTimeOffset? RetryAfter);
