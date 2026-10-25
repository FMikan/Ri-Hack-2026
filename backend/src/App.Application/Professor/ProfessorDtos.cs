using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using App.Domain.Content;
using App.Domain.Professor;

namespace App.Application.Professor;

// Mirrors docs/tehnicki-dokument.md §6.12. Keep frontend/src/types/api.ts in sync.

public record SendProfessorMessageRequest
{
    public Guid? ChatId { get; init; } // null starts a new chat

    [Required]
    [StringLength(ProfessorRules.MessageMaxLength)]
    public required string Text { get; init; }
}

public record CreateProfessorStudyRequest
{
    [Required]
    public required Guid MessageId { get; init; } // a professor answer that has a topic
}

public record ProfessorQuizAnswerRequest
{
    [Required]
    public required int Index { get; init; }

    public JsonElement Payload { get; init; } // AnswerPayload of the question's type; anything else is wrong
}

public record ProfessorChatSummaryDto(Guid Id, string Title, DateTimeOffset UpdatedAt);

public record ProfessorMessageDto(
    Guid Id, ProfessorRole Role, string Text, string? Topic, ProfessorScope? Scope, Guid? LessonId, Guid? QuizId, DateTimeOffset CreatedAt);

public record ProfessorChatDto(Guid Id, string Title, ProfessorMessageDto[] Messages);

// The stream's events: "delta" (ProfessorDeltaDto) while the answer is written, then "done" (ProfessorTurnDto) once
// both messages are saved, or "error" (ProfessorStreamErrorDto, shaped like Problem Details) and nothing is saved.
public record ProfessorDeltaDto(string Text);

public record ProfessorTurnDto(ProfessorChatSummaryDto Chat, ProfessorMessageDto Question, ProfessorMessageDto Answer);

public record ProfessorStreamErrorDto(string Type, string Title, string Detail, int Status);

public record ProfessorEvent(string Type, object Data);

public record ProfessorLessonDto(
    Guid Id, Guid ChatId, Guid MessageId, string Topic, string Title, string Summary, ProfessorCard[] Cards, Guid? QuizId, DateTimeOffset CreatedAt);

public record ProfessorQuizResultDto(int Index, JsonElement Payload, bool IsCorrect, JsonElement Answer, string Explanation);

public record ProfessorQuizQuestionDto(int Index, QuestionType Type, string? Language, JsonObject Content, ProfessorQuizResultDto? Result);

public record ProfessorQuizDto(
    Guid Id, Guid ChatId, Guid MessageId, string Topic, string Title, ProfessorQuizQuestionDto[] Questions, Guid? LessonId, DateTimeOffset CreatedAt);
