using System.Security.Claims;
using App.Application.Me;

namespace App.Api.Endpoints;

public static class MeEndpoints
{
    public static void MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/me").WithTags("Me").RequireAuthorization();

        group.MapGet("/", (ClaimsPrincipal user, MeService me, CancellationToken ct) => me.GetAsync(user.UserId(), ct));

        group.MapPatch("/", (UpdateMeRequest request, ClaimsPrincipal user, MeService me, CancellationToken ct) =>
            me.UpdateAsync(user.UserId(), request, ct))
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/topics", async (SetTopicsRequest request, ClaimsPrincipal user, MeService me, CancellationToken ct) =>
        {
            await me.SetTopicsAsync(user.UserId(), request.TopicIds, ct);
            return TypedResults.NoContent();
        })
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/dashboard", (ClaimsPrincipal user, MeService me, CancellationToken ct) => me.GetDashboardAsync(user.UserId(), ct));

        // The ledger, newest first: what paid how many coins, XP and SP, and for what (referenceId = match, lesson, ...).
        group.MapGet("/rewards", (ClaimsPrincipal user, MeService me, CancellationToken ct, int limit = 50) =>
            me.GetRewardsAsync(user.UserId(), limit, ct));

        group.MapGet("/knowledge-map", (ClaimsPrincipal user, MeService me, CancellationToken ct) => me.GetKnowledgeMapAsync(user.UserId(), ct));

        // "Due for review: n" with the concepts, most overdue first; practice them with POST /api/matches/practice { conceptIds }.
        group.MapGet("/progress/due", (ClaimsPrincipal user, MeService me, CancellationToken ct) => me.GetDueAsync(user.UserId(), ct));
    }
}
