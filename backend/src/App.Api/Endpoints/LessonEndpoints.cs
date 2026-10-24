using System.Security.Claims;
using App.Application.Lessons;

namespace App.Api.Endpoints;

public static class LessonEndpoints
{
    public static void MapLessonEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lessons").WithTags("Lessons").RequireAuthorization();

        // Builds the cards (AI, cached per concept and band, ≤ 8 s) and picks the three check questions.
        group.MapPost("/", async (StartLessonRequest request, ClaimsPrincipal user, LessonService lessons, CancellationToken ct) =>
            TypedResults.Created((string?)null, await lessons.StartAsync(user.UserId(), request.MatchId, ct)))
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}", (Guid id, ClaimsPrincipal user, LessonService lessons, CancellationToken ct) =>
            lessons.GetAsync(user.UserId(), id, ct))
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/check", (Guid id, SubmitLessonCheckRequest request, ClaimsPrincipal user, LessonService lessons, CancellationToken ct) =>
            lessons.CheckAsync(user.UserId(), id, request, ct))
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
