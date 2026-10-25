using System.Security.Claims;
using App.Application.Subscription;

namespace App.Api.Endpoints;

public static class SubscriptionEndpoints
{
    public static void MapSubscriptionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/subscription").WithTags("Subscription").RequireAuthorization();

        // My plan, the prices, today's AI Professor use and whether today's coins are claimed.
        group.MapGet("/", (ClaimsPrincipal user, SubscriptionService subscriptions, CancellationToken ct) =>
            subscriptions.GetAsync(user.UserId(), ct));

        // A simulated card payment: nothing is charged (hackathon). Paying while Senior adds the time on top.
        group.MapPost("/", (SubscribeRequest request, ClaimsPrincipal user, SubscriptionService subscriptions, CancellationToken ct) =>
            subscriptions.SubscribeAsync(user.UserId(), request, ct))
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status402PaymentRequired);

        // Stops the renewal; Senior stays until premiumUntil.
        group.MapDelete("/", (ClaimsPrincipal user, SubscriptionService subscriptions, CancellationToken ct) =>
            subscriptions.CancelAsync(user.UserId(), ct))
            .ProducesProblem(StatusCodes.Status409Conflict);

        // 25 coins once a day for a Senior developer; answers with the updated MeDto.
        group.MapPost("/daily-coins", (ClaimsPrincipal user, SubscriptionService subscriptions, CancellationToken ct) =>
            subscriptions.ClaimDailyCoinsAsync(user.UserId(), ct))
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
