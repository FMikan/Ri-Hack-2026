using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using App.Domain.Content;

namespace App.Application.Content;

public record TopicDto(Guid Id, string Slug, string Name, string Description, int ConceptCount);

public record ConceptDto(Guid Id, string Slug, string Name, Guid TopicId);

// The question as a player sees it: content without "answer" (docs §6.1).
public record QuestionPublicDto(Guid Id, QuestionType Type, int Difficulty, string? Language, int TimeLimitMs, int BasePoints, JsonObject Content);

public record AnswerKeyDto(JsonNode Answer, string Remember);

// Source "ai" (cached on the question) or "static" (generator unavailable: only `remember`, the why fields are empty).
public record ExplanationDto(string WhyCorrect, string WhyDistractor, string Remember, string Source);

// GET /api/questions/{id}: the key only for players who answered the question.
public record QuestionDetailDto(Guid Id, QuestionType Type, int Difficulty, string? Language, int TimeLimitMs, int BasePoints, JsonObject Content, AnswerKeyDto? Key);

public record ReportQuestionRequest
{
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public required string Reason { get; init; }
}
