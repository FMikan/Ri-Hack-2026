using System.Security.Claims;
using App.Application.Leaderboards;
using App.Domain;

namespace App.Api.Endpoints;

public static class LeaderboardEndpoints
{
    public static void MapLeaderboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").WithTags("Leaderboards").RequireAuthorization();

        // board: global | season | weekly | topic (with ?topic=<slug>) | friends. Top 100 plus my own row and its neighbours.
        group.MapGet("/leaderboards/{board}", (string board, ClaimsPrincipal user, LeaderboardService leaderboards, CancellationToken ct, string? topic = null) =>
            Enum.TryParse<Board>(board, ignoreCase: true, out var parsed) && !int.TryParse(board, out _)
                ? leaderboards.GetAsync(user.UserId(), parsed, topic, ct)
                : throw new AppException(ErrorCodes.LeaderboardNotFound, "This leaderboard does not exist.", 404))
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/seasons/current", (LeaderboardService leaderboards, CancellationToken ct) => leaderboards.CurrentSeasonAsync(ct));
    }
}
