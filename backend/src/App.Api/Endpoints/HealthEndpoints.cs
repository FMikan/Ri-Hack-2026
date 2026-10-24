using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace App.Api.Endpoints;

public record HealthResponse(string Status, Dictionary<string, string> Checks);

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        // Liveness: the process is up and serving requests. Runs no checks.
        app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false, ResponseWriter = WriteJson });

        // Readiness: dependencies (PostgreSQL) are reachable. 503 when any check fails.
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteJson,
        });
    }

    // Status names only: never leak exception messages or connection details.
    private static Task WriteJson(HttpContext context, HealthReport report) =>
        context.Response.WriteAsJsonAsync(new HealthResponse(
            report.Status.ToString(),
            report.Entries.ToDictionary(e => e.Key, e => e.Value.Status.ToString())));
}
