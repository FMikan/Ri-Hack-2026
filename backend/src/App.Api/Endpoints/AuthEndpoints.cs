using App.Application.Auth;

namespace App.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async (RegisterRequest request, AuthService auth, CancellationToken ct) =>
            TypedResults.Created((string?)null, await auth.RegisterAsync(request, ct)))
            .RequireRateLimiting(RateLimits.Register)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/login", async (LoginRequest request, AuthService auth, CancellationToken ct) =>
            TypedResults.Ok(await auth.LoginAsync(request, ct)))
            .RequireRateLimiting(RateLimits.Login)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        // The Google client id for the sign-in button, or null when Google sign-in is off (Auth:GoogleClientId unset).
        group.MapGet("/config", (AuthService auth) => auth.Config());

        // The ID token from Google's button: signs in, links a verified e-mail or creates the account (401 google-invalid).
        group.MapPost("/google", async (GoogleLoginRequest request, AuthService auth, CancellationToken ct) =>
            TypedResults.Ok(await auth.GoogleLoginAsync(request.IdToken, ct)))
            .RequireRateLimiting(RateLimits.Login)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/refresh", async (RefreshRequest request, AuthService auth, CancellationToken ct) =>
            TypedResults.Ok(await auth.RefreshAsync(request.RefreshToken, ct)))
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", async (RefreshRequest request, AuthService auth, CancellationToken ct) =>
        {
            await auth.LogoutAsync(request.RefreshToken, ct);
            return TypedResults.NoContent();
        });
    
        // Always 202, whether or not the e-mail has an account. The link points at {Frontend:BaseUrl}/reset-password?token=...
        group.MapPost("/forgot-password", async (ForgotPasswordRequest request, AuthService auth, CancellationToken ct) =>
        {
            await auth.ForgotPasswordAsync(request.Email, ct);
            return TypedResults.Accepted((string?)null);
        })
            .RequireRateLimiting(RateLimits.Login)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/reset-password", async (ResetPasswordRequest request, AuthService auth, CancellationToken ct) =>
        {
            await auth.ResetPasswordAsync(request, ct);
            return TypedResults.NoContent();
        })
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
