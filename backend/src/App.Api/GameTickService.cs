using App.Application.Matches;
using App.Application.Queue;
using App.Application.Seasons;
using App.Domain;

namespace App.Api;

// Every second: advance matches whose phase deadline passed (veto turns, timeouts, interstitials, the next question).
// Every second tick: the ranked matchmaker. Every minute: housekeeping (Grandmasters, weekly and seasonal snapshots).
// One failing match is logged and skipped; nothing stops the loop or the host.
// ponytail: single API instance; add a leader lock (pg_advisory_lock) if a second instance ever appears.
public class GameTickService(IServiceScopeFactory scopes, TimeProvider time, ILogger<GameTickService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1), time);
        DateOnly? lastDay = null;
        for (var tick = 0L; await timer.WaitForNextTickAsync(stoppingToken); tick++)
        {
            if (tick % 60 == 0)
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    var housekeeping = scope.ServiceProvider.GetRequiredService<HousekeepingService>();
                    await housekeeping.RunAsync(stoppingToken);
                    // Once a local day (and once at startup): question difficulty auto-calibration.
                    var today = Clock.Today(time.GetUtcNow());
                    if (lastDay != today)
                    {
                        await housekeeping.DailyAsync(stoppingToken);
                        lastDay = today;
                    }
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    logger.LogError(e, "Housekeeping failed");
                }
            }

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
