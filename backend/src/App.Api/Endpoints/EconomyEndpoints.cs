using System.Security.Claims;
using App.Application.Daily;
using App.Application.Economy;

namespace App.Api.Endpoints;

public static class EconomyEndpoints
{
    public static void MapEconomyEndpoints(this IEndpointRouteBuilder app)
    {
        var shop = app.MapGroup("/api/shop").WithTags("Shop").RequireAuthorization();

        shop.MapGet("/", (ClaimsPrincipal user, ShopService shopService, CancellationToken ct) => shopService.ListAsync(user.UserId(), ct));

        shop.MapPost("/purchase", (PurchaseRequest request, ClaimsPrincipal user, ShopService shopService, CancellationToken ct) =>
            shopService.PurchaseAsync(user.UserId(), request.ItemId, ct))
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var daily = app.MapGroup("/api/daily-challenge").WithTags("Daily").RequireAuthorization();

        // The first request of the day starts my 60 s.
        daily.MapGet("/", (ClaimsPrincipal user, DailyService service, CancellationToken ct) => service.TodayAsync(user.UserId(), ct))
            .ProducesProblem(StatusCodes.Status404NotFound);

        daily.MapGet("/{day}", (DateOnly day, ClaimsPrincipal user, DailyService service, CancellationToken ct) => service.DayAsync(user.UserId(), day, ct))
            .ProducesProblem(StatusCodes.Status404NotFound);

        daily.MapPost("/attempt", (DailyAttemptRequest request, ClaimsPrincipal user, DailyService service, CancellationToken ct) =>
            service.AttemptAsync(user.UserId(), request, ct))
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
