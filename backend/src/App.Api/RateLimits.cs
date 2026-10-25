using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;

namespace App.Api;

// Fixed windows per client IP (auth) or per user (answers, the AI Professor). Rejections are 429 Problem Details with Retry-After.
// The IP is the player's only because forwarded headers are applied first (Program.cs).
public static class RateLimits
{
    public const string Login = "login", Register = "register", Answers = "answers", Professor = "professor";

    public static IServiceCollection AddAppRateLimits(this IServiceCollection services) => services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy(Login, context => Window(Ip(context), 10, TimeSpan.FromMinutes(1)));
        options.AddPolicy(Register, context => Window(Ip(context), 5, TimeSpan.FromMinutes(1)));
        options.AddPolicy(Answers, context => Window(context.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? Ip(context), 5, TimeSpan.FromSeconds(1)));
        // Every professor message, lesson and quiz is a paid model call.
        options.AddPolicy(Professor, context => Window(context.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? Ip(context), 10, TimeSpan.FromMinutes(1)));
        options.OnRejected = async (context, cancellationToken) =>
        {
            var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var wait) ? wait : TimeSpan.FromMinutes(1);
            context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
            await context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context.HttpContext,
                ProblemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too many requests",
                    Detail = $"Too many attempts. Try again in {Math.Ceiling(retryAfter.TotalSeconds)} s.",
                },
            });
        };
    });

    private static string Ip(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static RateLimitPartition<string> Window(string key, int permits, TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = window });
}
