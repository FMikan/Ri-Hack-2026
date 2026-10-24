using System.Security.Claims;
using App.Application.Content;

namespace App.Api.Endpoints;

public static class ContentEndpoints
{
    public static void MapContentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/topics").WithTags("Content");

        // Public: the onboarding screen lists topics before the user has an account.
        group.MapGet("/", (ContentService content, CancellationToken ct) => content.ListTopicsAsync(ct));

        group.MapGet("/{slug}/concepts", (string slug, ContentService content, CancellationToken ct) => content.ListConceptsAsync(slug, ct))
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status404NotFound);

        var questions = app.MapGroup("/api/questions").WithTags("Content").RequireAuthorization();

        // The key is included only for a player who answered the question (knowledge map, history).
        questions.MapGet("/{id:guid}", (Guid id, ClaimsPrincipal user, ContentService content, CancellationToken ct) =>
            content.GetQuestionAsync(user.UserId(), id, ct))
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Called on its own from the analysis screen, never from match polling. The first call may wait for the AI (≤ 8 s).
        questions.MapGet("/{id:guid}/explanation", (Guid id, ClaimsPrincipal user, ContentService content, CancellationToken ct) =>
            content.ExplainAsync(user.UserId(), id, ct))
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        questions.MapPost("/{id:guid}/report", async (Guid id, ReportQuestionRequest request, ClaimsPrincipal user, ContentService content, CancellationToken ct) =>
        {
            await content.ReportAsync(user.UserId(), id, request.Reason, ct);
            return TypedResults.NoContent();
        })
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
