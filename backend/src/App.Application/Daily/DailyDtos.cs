using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using App.Application.Content;
using App.Application.Me;
using App.Domain.Progress;

namespace App.Application.Daily;

// Mirrors docs/tehnicki-dokument.md §6.8. Keep frontend/src/types/api.ts in sync.

public record DailyTasksDto(DailyTaskDto[] Tasks, bool BonusAwarded);

public record DailyAttemptRequest
{
    public JsonElement Payload { get; init; } // { line, fixId }; anything else is wrong

    [Range(0, 600_000)]
    public required int ClientElapsedMs { get; init; }
}

public record DailyAttemptDto(JsonNode? Payload, bool IsCorrect, double Fraction, int Points, int ElapsedMs, bool TimedOut, AnswerLabel Label, int? Rank);

public record DailyLeaderboardEntryDto(int Rank, UserRefDto User, bool IsCorrect, int ElapsedMs);

// question.timeLimitMs is 60 000; key and explanation only after my attempt; startedAt is when I opened it.
public record DailyChallengeDto(
    DateOnly Day, QuestionPublicDto Question, DateTimeOffset? StartedAt, DateTimeOffset ServerTime, bool Attempted,
    DailyAttemptDto? MyAttempt, AnswerKeyDto? Key, ExplanationDto? Explanation, DailyLeaderboardEntryDto[] Leaderboard);
