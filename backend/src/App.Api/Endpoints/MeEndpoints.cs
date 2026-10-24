using System.Security.Claims;
using App.Application.Daily;
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

        group.MapGet("/daily-tasks", (ClaimsPrincipal user, DailyService daily, CancellationToken ct) => daily.TasksAsync(user.UserId(), ct));

        // { avatarFrame?, banner?, boardTheme?, nameColor?, cardBackground?, titleId? }: an owned item (earned title) id, or null to take it off.
        group.MapPut("/equipment", (Dictionary<string, Guid?> slots, ClaimsPrincipal user, MeService me, CancellationToken ct) =>
            me.SetEquipmentAsync(user.UserId(), slots, ct))
            .ProducesProblem(StatusCodes.Status409Conflict);

        // Win rate, accuracy and time per question type, the most common blunder type, rating history.
        group.MapGet("/stats", (ClaimsPrincipal user, ProfileService profiles, CancellationToken ct) => profiles.GetStatsAsync(user.UserId(), ct));

        // Every achievement: earnedAt once earned, otherwise progress/target. Newly reached ones are recorded on read.
        group.MapGet("/achievements", (ClaimsPrincipal user, ProfileService profiles, CancellationToken ct) => profiles.GetAchievementsAsync(user.UserId(), ct));

        // Season, knowledge and weekly titles; equip one with PUT /api/me/equipment { titleId }.
        group.MapGet("/titles", (ClaimsPrincipal user, ProfileService profiles, CancellationToken ct) => profiles.GetTitlesAsync(user.UserId(), ct));

        group.MapGet("/knowledge-map", (ClaimsPrincipal user, MeService me, CancellationToken ct) => me.GetKnowledgeMapAsync(user.UserId(), ct));

        // "Due for review: n" with the concepts, most overdue first; practice them with POST /api/matches/practice { conceptIds }.
        group.MapGet("/progress/due", (ClaimsPrincipal user, MeService me, CancellationToken ct) => me.GetDueAsync(user.UserId(), ct));
    }
}
