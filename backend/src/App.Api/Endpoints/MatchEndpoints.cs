using System.Security.Claims;
using App.Application.Matches;

namespace App.Api.Endpoints;

public static class MatchEndpoints
{
    public static void MapMatchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/matches").WithTags("Matches").RequireAuthorization();

        group.MapPost("/bot", async (StartBotMatchRequest request, ClaimsPrincipal user, MatchService matches, CancellationToken ct) =>
            TypedResults.Created((string?)null, await matches.StartBotAsync(user.UserId(), request, ct)))
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/practice", async (StartPracticeRequest request, ClaimsPrincipal user, MatchService matches, CancellationToken ct) =>
            TypedResults.Created((string?)null, await matches.StartPracticeAsync(user.UserId(), request, ct)))
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/calibration", async (StartCalibrationRequest request, ClaimsPrincipal user, MatchService matches, CancellationToken ct) =>
            TypedResults.Created((string?)null, await matches.StartCalibrationAsync(user.UserId(), request, ct)))
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // Polled about once a second while the match runs; also marks the player as connected.
        group.MapGet("/{id:guid}", (Guid id, ClaimsPrincipal user, MatchService matches, CancellationToken ct) =>
            matches.GetStateAsync(user.UserId(), id, ct))
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/answers", (Guid id, SubmitAnswerRequest request, ClaimsPrincipal user, MatchService matches, CancellationToken ct) =>
            matches.SubmitAnswerAsync(user.UserId(), id, request, ct))
            .RequireRateLimiting(RateLimits.Answers)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}/review", (Guid id, ClaimsPrincipal user, MatchService matches, CancellationToken ct) =>
            matches.ReviewAsync(user.UserId(), id, ct))
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/next", (Guid id, ClaimsPrincipal user, MatchService matches, CancellationToken ct) =>
            matches.NextAsync(user.UserId(), id, ct))
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/resign", (Guid id, ClaimsPrincipal user, MatchService matches, CancellationToken ct) =>
            matches.ResignAsync(user.UserId(), id, ct))
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapGet("/api/bots", (ClaimsPrincipal user, MatchService matches, CancellationToken ct) => matches.ListBotsAsync(user.UserId(), ct))
            .WithTags("Matches")
            .RequireAuthorization();

        app.MapGet("/api/me/matches", (ClaimsPrincipal user, MatchService matches, CancellationToken ct, int limit = 50) =>
            matches.ListMineAsync(user.UserId(), limit, ct))
            .WithTags("Me")
            .RequireAuthorization();
    }
}
