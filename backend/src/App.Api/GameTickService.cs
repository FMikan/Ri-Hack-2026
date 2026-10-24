using App.Application.Matches;
using App.Application.Queue;

namespace App.Api;

// Every second: advance matches whose phase deadline passed (veto turns, timeouts, interstitials, the next question).
// Every second tick: the ranked matchmaker. One failing match is logged and skipped; nothing stops the loop or the host.
// ponytail: single API instance; add a leader lock (pg_advisory_lock) if a second instance ever appears.
public class GameTickService(IServiceScopeFactory scopes, TimeProvider time, ILogger<GameTickService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1), time);
        for (var tick = 0L; await timer.WaitForNextTickAsync(stoppingToken); tick++)
        {
            if (tick % 2 == 0)
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<QueueService>().RunMatchmakingAsync(stoppingToken);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    logger.LogError(e, "Matchmaking failed");
                }
            }

            try
            {
                List<Guid> due;
                using (var scope = scopes.CreateScope())
                    due = await scope.ServiceProvider.GetRequiredService<MatchService>().DueMatchIdsAsync(stoppingToken);

                foreach (var matchId in due)
                {
                    try
                    {
                        using var scope = scopes.CreateScope();
                        await scope.ServiceProvider.GetRequiredService<MatchService>().AdvanceAsync(matchId, stoppingToken);
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        logger.LogError(e, "Advancing match {MatchId} failed", matchId);
                    }
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Game tick failed");
            }
        }
    }
}
