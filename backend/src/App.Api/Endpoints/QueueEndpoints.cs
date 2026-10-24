using System.Security.Claims;
using App.Application.Queue;

namespace App.Api.Endpoints;

public static class QueueEndpoints
{
    public static void MapQueueEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/queue").WithTags("Queue").RequireAuthorization();

        group.MapPost("/", (ClaimsPrincipal user, QueueService queue, CancellationToken ct) => queue.JoinAsync(user.UserId(), ct))
            .ProducesProblem(StatusCodes.Status409Conflict);

        // Polled about once a second while queued; status "matched" carries the matchId to open.
        group.MapGet("/", (ClaimsPrincipal user, QueueService queue, CancellationToken ct) => queue.StateAsync(user.UserId(), ct));

        // Also declines a pending proposal; the other player keeps their place and wait time.
        group.MapDelete("/", async (ClaimsPrincipal user, QueueService queue, CancellationToken ct) =>
        {
            await queue.LeaveAsync(user.UserId(), ct);
            return TypedResults.NoContent();
        });

        group.MapPost("/ghost", async (ClaimsPrincipal user, QueueService queue, CancellationToken ct) =>
            TypedResults.Created((string?)null, await queue.AcceptGhostAsync(user.UserId(), ct)))
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/ghost/decline", (ClaimsPrincipal user, QueueService queue, CancellationToken ct) =>
            queue.DeclineGhostAsync(user.UserId(), ct))
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/proposals/{id:guid}/accept", (Guid id, ClaimsPrincipal user, QueueService queue, CancellationToken ct) =>
            queue.AcceptAsync(user.UserId(), id, ct))
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
