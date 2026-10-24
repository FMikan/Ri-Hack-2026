using App.Domain;
using Microsoft.EntityFrameworkCore;

namespace App.Application.Seasons;

// The minute tick (docs §8). Every step is idempotent, so a late or repeated run is harmless.
public class HousekeepingService(IAppDbContext db, TimeProvider time)
{
    public async Task RunAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        // Unjoined challenges are kept a day after they expire, so a late guest reads "expired" rather than "not found".
        await db.FriendChallenges.Where(c => c.MatchId == null && c.ExpiresAt < now.AddDays(-1)).ExecuteDeleteAsync(ct);
        await db.RefreshTokens.Where(t => t.ExpiresAt < now).ExecuteDeleteAsync(ct);
        await RefreshGrandmastersAsync(ct);
    }

    // The global top 100 by Elo among players with 10+ ranked matches (idea §14); only changed rows are written.
    private Task RefreshGrandmastersAsync(CancellationToken ct)
    {
        var top = db.Users.Where(u => u.RankedMatches >= GameRules.ProvisionalMatches)
            .OrderByDescending(u => u.Rating).ThenBy(u => u.UsernameNormalized)
            .Take(GameRules.GrandmasterTop)
            .Select(u => u.Id);
        return db.Users.Where(u => u.IsGrandmaster != top.Contains(u.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsGrandmaster, u => !u.IsGrandmaster), ct);
    }
}
