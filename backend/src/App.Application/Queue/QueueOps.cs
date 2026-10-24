using App.Domain.Matchmaking;
using Microsoft.EntityFrameworkCore;

namespace App.Application.Queue;

public static class QueueOps
{
    // Leaving the queue (also when any other match starts). A pending proposal is declined under its row lock,
    // and the other player goes back to waiting with the time already waited (idea §4).
    public static async Task LeaveQueueAsync(this IAppDbContext db, Guid userId, CancellationToken ct)
    {
        var entry = await db.QueueEntries.FirstOrDefaultAsync(q => q.UserId == userId, ct);
        if (entry is null) return;

        var own = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            if (entry.ProposalId is { } proposalId)
            {
                await db.Database.SqlQuery<int>($"select 1 as \"Value\" from match_proposals where id = {proposalId} for update").ToListAsync(ct);
                var proposal = await db.MatchProposals.FirstAsync(p => p.Id == proposalId, ct);
                if (proposal.Status == ProposalStatus.Pending)
                {
                    proposal.Status = ProposalStatus.Declined;
                    var other = proposal.UserA == userId ? proposal.UserB : proposal.UserA;
                    await db.QueueEntries.Where(q => q.UserId == other && q.ProposalId == proposalId)
                        .ExecuteUpdateAsync(s => s.SetProperty(q => q.Status, QueueStatus.Waiting).SetProperty(q => q.ProposalId, (Guid?)null), ct);
                }
            }
            db.QueueEntries.Remove(entry);
            await db.SaveChangesAsync(ct);
            if (own is not null) await own.CommitAsync(ct);
        }
        finally
        {
            if (own is not null) await own.DisposeAsync();
        }
    }
}
