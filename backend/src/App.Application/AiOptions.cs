using Microsoft.Extensions.Logging;

namespace App.Application;

// Every live AI call is bounded (Ai:TimeoutMs, 8 s by default). On a timeout or any failure the caller gets null and
// uses its fallback, so a player never waits on an empty screen (idea §16).
public record AiOptions(TimeSpan Timeout)
{
    public async Task<T?> TryAsync<T>(Func<CancellationToken, Task<T>> call, ILogger logger, CancellationToken cancellationToken) where T : class
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        try
        {
            return await call(timeout.Token);
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested)
        {
            if (e is NotSupportedException) logger.LogInformation("{Reason} Using the fallback.", e.Message);
            else logger.LogWarning(e, "AI call failed or timed out; using the fallback");
            return null;
        }
    }
}
