using System.Net.ServerSentEvents;
using System.Security.Claims;
using System.Text.Json;
using App.Application.Professor;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace App.Api.Endpoints;

public static class ProfessorEndpoints
{
    public static void MapProfessorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/professor").WithTags("Professor").RequireAuthorization();

        group.MapGet("/chats", (ClaimsPrincipal user, ProfessorService professor, CancellationToken ct) =>
            professor.ListChatsAsync(user.UserId(), ct));

        group.MapGet("/chats/{id:guid}", (Guid id, ClaimsPrincipal user, ProfessorService professor, CancellationToken ct) =>
            professor.GetChatAsync(user.UserId(), id, ct))
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Server-sent events: "delta" chunks of the answer, then "done" with the saved turn, or "error" (docs §6.12).
        // A missing chat or AI is checked first and answers with ordinary Problem Details instead of a stream.
        group.MapPost("/messages", async (SendProfessorMessageRequest request, ClaimsPrincipal user, ProfessorService professor,
                IOptions<JsonOptions> json, CancellationToken ct) =>
            {
                var turn = await professor.PrepareTurnAsync(user.UserId(), request, ct);
                return TypedResults.ServerSentEvents(Events(professor.StreamTurnAsync(turn, ct), json.Value.SerializerOptions));
            })
            .RequireRateLimiting(RateLimits.Professor)
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        // Returns the answer's lesson when it has one; otherwise the AI writes it (up to ~30 s).
        group.MapPost("/lessons", (CreateProfessorStudyRequest request, ClaimsPrincipal user, ProfessorService professor, CancellationToken ct) =>
            professor.CreateLessonAsync(user.UserId(), request.MessageId, ct))
            .RequireRateLimiting(RateLimits.Professor)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/lessons/{id:guid}", (Guid id, ClaimsPrincipal user, ProfessorService professor, CancellationToken ct) =>
            professor.GetLessonAsync(user.UserId(), id, ct))
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/quizzes", (CreateProfessorStudyRequest request, ClaimsPrincipal user, ProfessorService professor, CancellationToken ct) =>
            professor.CreateQuizAsync(user.UserId(), request.MessageId, ct))
            .RequireRateLimiting(RateLimits.Professor)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/quizzes/{id:guid}", (Guid id, ClaimsPrincipal user, ProfessorService professor, CancellationToken ct) =>
            professor.GetQuizAsync(user.UserId(), id, ct))
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/quizzes/{id:guid}/answers", (Guid id, ProfessorQuizAnswerRequest request, ClaimsPrincipal user, ProfessorService professor, CancellationToken ct) =>
            professor.AnswerAsync(user.UserId(), id, request, ct))
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    // Each event's data is its DTO as JSON with the API's settings (camelCase, snake_case enums), one line per event.
    private static async IAsyncEnumerable<SseItem<string>> Events(IAsyncEnumerable<ProfessorEvent> events, JsonSerializerOptions options)
    {
        await foreach (var e in events)
            yield return new SseItem<string>(JsonSerializer.Serialize(e.Data, e.Data.GetType(), options), e.Type);
    }
}
