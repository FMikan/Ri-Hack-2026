using App.Application.Auth;

namespace App.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async (RegisterRequest request, AuthService auth, CancellationToken ct) =>
            TypedResults.Created((string?)null, await auth.RegisterAsync(request, ct)))
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/login", async (LoginRequest request, AuthService auth, CancellationToken ct) =>
            TypedResults.Ok(await auth.LoginAsync(request, ct)))
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/refresh", async (RefreshRequest request, AuthService auth, CancellationToken ct) =>
            TypedResults.Ok(await auth.RefreshAsync(request.RefreshToken, ct)))
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", async (RefreshRequest request, AuthService auth, CancellationToken ct) =>
        {
            await auth.LogoutAsync(request.RefreshToken, ct);
            return TypedResults.NoContent();
        });
    }
}
