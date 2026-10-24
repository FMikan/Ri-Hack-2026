using App.Application.Content;
using App.Application.Matches;
using App.Application.Me;
using App.Domain;
using App.Domain.Matches;
using App.Domain.Matchmaking;
using Microsoft.EntityFrameworkCore;

namespace App.Application.Queue;

// The ranked queue (idea §4): join, wait, "opponent found", both accept, the match starts with the veto.
public class QueueService(IAppDbContext db, MatchService matches, TimeProvider time)
{
    public async Task<QueueStateDto> JoinAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.Include(u => u.Topics).FirstAsync(u => u.Id == userId, ct);
        if (user.Topics.Count is < GameRules.MinTopics or > GameRules.MaxTopics)
            throw new AppException(ErrorCodes.TopicsCount, $"Pick {GameRules.MinTopics} to {GameRules.MaxTopics} topics before you queue.");
        if (user.CurrentMatchId is { } current && await db.Matches.AnyAsync(m => m.Id == current && m.Phase != MatchPhase.Done, ct))
            throw new AppException(ErrorCodes.MatchAlreadyInMatch, "Finish or resign your current match first.");
        if (await db.QueueEntries.AnyAsync(q => q.UserId == userId, ct))
            throw AlreadyIn();

        db.QueueEntries.Add(new QueueEntry { UserId = userId, Rating = user.Rating, TopicIds = [.. user.Topics.Select(t => t.TopicId)], EnteredAt = time.GetUtcNow() });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw AlreadyIn(); // a double click won the primary key
        }
        return await StateAsync(userId, ct);
    }

    public async Task<QueueStateDto> StateAsync(Guid userId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var entry = await db.QueueEntries.AsNoTracking().FirstOrDefaultAsync(q => q.UserId == userId, ct);
        if (entry is null)
        {
            // Out of the queue: either idle, or the accepted proposal became a ranked match that is still running.
            var matchId = await db.Users.Where(u => u.Id == userId)
                .Select(u => db.Matches.Where(m => m.Id == u.CurrentMatchId && m.Kind == MatchKind.Ranked && m.Phase != MatchPhase.Done).Select(m => (Guid?)m.Id).FirstOrDefault())
                .FirstOrDefaultAsync(ct);
            return new QueueStateDto(matchId is null ? QueuePhase.Idle : QueuePhase.Matched, null, now, null, [], null, null, false, matchId);
        }

        QueueProposalDto? proposal = null;
        if (entry.Status == QueueStatus.Proposed && entry.ProposalId is { } proposalId)
        {
            var p = await db.MatchProposals.AsNoTracking().FirstAsync(x => x.Id == proposalId, ct);
            var opponent = await db.Users.FirstAsync(u => u.Id == (p.UserA == userId ? p.UserB : p.UserA), ct);
            proposal = new QueueProposalDto(p.Id, UserRefDto.From(opponent), p.ExpiresAt, (p.UserA == userId ? p.AAcceptedAt : p.BAcceptedAt) is not null);
        }
        var waited = now - entry.EnteredAt;
        var ghost = entry.GhostOfferMatchId is { } source ? await GhostOfferAsync(source, entry.GhostOfferSeat!.Value, ct) : null;
        // After 30 s: the ghost when the matchmaker found a recording, otherwise (or once it is declined) the bot.
        return new QueueStateDto(
            entry.Status == QueueStatus.Proposed ? QueuePhase.Proposed : QueuePhase.Waiting, entry.EnteredAt, now,
            MatchmakingRules.Range(waited), [.. entry.TopicIds], proposal, ghost,
            entry.Status == QueueStatus.Waiting && ghost is null && waited >= TimeSpan.FromSeconds(GameRules.BotOfferAfterSeconds), null);
    }

    // The ghost duel replaces waiting: the player leaves the queue and plays the recording.
    public async Task<MatchStateDto> AcceptGhostAsync(Guid userId, CancellationToken ct)
    {
        var entry = await db.QueueEntries.AsNoTracking().FirstOrDefaultAsync(q => q.UserId == userId, ct);
        if (entry is not { GhostOfferMatchId: { } source, GhostOfferSeat: { } seat })
            throw NoGhostOffer();
        return await matches.StartGhostAsync(userId, source, seat, ct);
    }

    // Keeps waiting; the bot is offered instead and no other ghost in this wait.
    public async Task<QueueStateDto> DeclineGhostAsync(Guid userId, CancellationToken ct)
    {
        var updated = await db.QueueEntries.Where(q => q.UserId == userId && q.GhostOfferMatchId != null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(q => q.GhostOfferMatchId, (Guid?)null)
                .SetProperty(q => q.GhostOfferSeat, (int?)null)
                .SetProperty(q => q.GhostOfferDeclined, true), ct);
        if (updated == 0) throw NoGhostOffer();
        return await StateAsync(userId, ct);
    }

    public Task LeaveAsync(Guid userId, CancellationToken ct) => db.LeaveQueueAsync(userId, ct);

    // Under the proposal's row lock: two simultaneous accepts create exactly one match.
    public async Task<QueueStateDto> AcceptAsync(Guid userId, Guid proposalId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            await db.Database.SqlQuery<int>($"select 1 as \"Value\" from match_proposals where id = {proposalId} for update").ToListAsync(ct);
            var proposal = await db.MatchProposals.FirstOrDefaultAsync(p => p.Id == proposalId && (p.UserA == userId || p.UserB == userId), ct);
            if (proposal is not { Status: ProposalStatus.Pending } || proposal.ExpiresAt <= now)
                throw new AppException(ErrorCodes.ProposalExpired, "This match offer is no longer open.");

            if (proposal.UserA == userId) proposal.AAcceptedAt ??= now;
            else proposal.BAcceptedAt ??= now;
            if (proposal is { AAcceptedAt: not null, BAcceptedAt: not null })
            {
                proposal.Status = ProposalStatus.Matched;
                proposal.MatchId = await matches.CreateDuelAsync(proposal.UserA, proposal.UserB, MatchKind.Ranked, now, ct);
                await db.QueueEntries.Where(q => q.ProposalId == proposal.Id).ExecuteDeleteAsync(ct);
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        return await StateAsync(userId, ct);
    }

    // The tick (every 2 s): expire unanswered proposals, then pair waiting players, longest wait first.
    public async Task RunMatchmakingAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Who accepted goes back to waiting with the time already waited; who did not leaves the queue.
        var expired = await db.MatchProposals
            .FromSql($"select * from match_proposals where status = 'pending' and expires_at <= {now} for update skip locked")
            .ToListAsync(ct);
        foreach (var proposal in expired)
        {
            proposal.Status = ProposalStatus.Expired;
            foreach (var (user, accepted) in new[] { (proposal.UserA, proposal.AAcceptedAt), (proposal.UserB, proposal.BAcceptedAt) })
            {
                var entries = db.QueueEntries.Where(q => q.UserId == user && q.ProposalId == proposal.Id);
                if (accepted is null) await entries.ExecuteDeleteAsync(ct);
                else await entries.ExecuteUpdateAsync(s => s.SetProperty(q => q.Status, QueueStatus.Waiting).SetProperty(q => q.ProposalId, (Guid?)null), ct);
            }
        }
        await db.SaveChangesAsync(ct);

        // SKIP LOCKED: a player leaving or accepting right now is simply not paired in this round.
        var waiting = await db.QueueEntries
            .FromSql($"select * from queue_entries where status = 'waiting' for update skip locked")
            .OrderBy(q => q.EnteredAt)
            .ToListAsync(ct);
        var ids = waiting.Select(q => q.UserId).ToList();
        var busy = await db.Users.Where(u => ids.Contains(u.Id) && db.Matches.Any(m => m.Id == u.CurrentMatchId && m.Phase != MatchPhase.Done))
            .Select(u => u.Id).ToListAsync(ct);
        var open = waiting.Where(q => !busy.Contains(q.UserId)).ToList();

        var unpaired = new List<QueueEntry>(open);
        while (open.Count > 1)
        {
            var me = open[0];
            open.RemoveAt(0);
            var lastTwo = await LastRankedOpponentsAsync(me.UserId, ct);
            var pick = MatchmakingRules.Pick(Candidate(me), open.Select(Candidate), lastTwo, now);
            if (pick is null) continue;

            var other = open.First(q => q.UserId == pick.UserId);
            open.Remove(other);
            var proposal = new MatchProposal
            {
                UserA = me.UserId, UserB = other.UserId, CreatedAt = now, ExpiresAt = now.AddSeconds(GameRules.ProposalAcceptSeconds),
            };
            db.MatchProposals.Add(proposal);
            foreach (var entry in new[] { me, other }) (entry.Status, entry.ProposalId) = (QueueStatus.Proposed, proposal.Id);
            unpaired.Remove(me);
            unpaired.Remove(other);
        }

        // Nobody for 30 s: offer a recording once (idea §4).
        foreach (var entry in unpaired.Where(q => q.GhostOfferMatchId is null && !q.GhostOfferDeclined
                     && now - q.EnteredAt >= TimeSpan.FromSeconds(GameRules.GhostOfferAfterSeconds)))
        {
            if (await FindGhostAsync(entry, now, ct) is not { } offer) continue;
            (entry.GhostOfferMatchId, entry.GhostOfferSeat) = (offer.SourceMatchId, offer.Seat);
            db.GhostOffers.Add(new GhostOffer { UserId = entry.UserId, SourceMatchId = offer.SourceMatchId, OfferedAt = now });
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    // A finished ranked or ghost match with a real result, on one of my topics, at most 30 days old, never offered
    // to me before; GhostOffers.Find applies the rating range and the 30-day "seen" rule and takes the freshest.
    private async Task<(Guid SourceMatchId, int Seat)?> FindGhostAsync(QueueEntry entry, DateTimeOffset now, CancellationToken ct)
    {
        var since = now.AddDays(-GameRules.GhostRecordingMaxDays);
        var offered = db.GhostOffers.Where(o => o.UserId == entry.UserId).Select(o => o.SourceMatchId);
        var topics = entry.TopicIds;
        var rows = await db.MatchPlayers
            .Where(p => p.Kind == SeatKind.Human && p.UserId != null && p.UserId != entry.UserId)
            .Join(db.Matches, p => p.MatchId, m => m.Id, (p, m) => new { Player = p, Match = m })
            .Where(x => (x.Match.Kind == MatchKind.Ranked || x.Match.Kind == MatchKind.Ghost) && x.Match.FinishedAt >= since
                && (x.Match.ResultReason == MatchResultReason.Score || x.Match.ResultReason == MatchResultReason.TiebreakCorrect
                    || x.Match.ResultReason == MatchResultReason.TiebreakTime || x.Match.ResultReason == MatchResultReason.Draw)
                && x.Match.TopicId != null && topics.Contains(x.Match.TopicId.Value) && !offered.Contains(x.Match.Id))
            .OrderByDescending(x => x.Match.FinishedAt)
            .Take(50) // ponytail: newest 50 recordings; widen if the range and "seen" rule filter too many out
            .Select(x => new { MatchId = x.Match.Id, x.Player.Seat, UserId = x.Player.UserId!.Value, x.Player.DisplayRating, TopicId = x.Match.TopicId!.Value, FinishedAt = x.Match.FinishedAt!.Value })
            .ToListAsync(ct);
        if (rows.Count == 0) return null;

        var ids = rows.Select(r => r.MatchId).Distinct().ToList();
        var decks = (await db.MatchQuestions.Where(q => ids.Contains(q.MatchId)).Select(q => new { q.MatchId, q.QuestionId }).ToListAsync(ct))
            .ToLookup(q => q.MatchId, q => q.QuestionId);
        var seenSince = now.AddDays(-GameRules.SeenQuestionDays);
        var seen = (await db.Answers.Where(a => a.UserId == entry.UserId && a.CreatedAt >= seenSince).Select(a => a.QuestionId).Distinct().ToListAsync(ct)).ToHashSet();
        var pick = GhostOffers.Find(entry.Rating, now - entry.EnteredAt,
            rows.Select(r => new GhostCandidate(r.MatchId, r.UserId, r.DisplayRating, r.TopicId, r.FinishedAt, [.. decks[r.MatchId]])), seen);
        return pick is null ? null : (pick.SourceMatchId, rows.First(r => r.MatchId == pick.SourceMatchId && r.UserId == pick.UserId).Seat);
    }

    private async Task<GhostOfferDto> GhostOfferAsync(Guid sourceMatchId, int seat, CancellationToken ct)
    {
        var source = await db.Matches.Where(m => m.Id == sourceMatchId)
            .Select(m => new { m.FinishedAt, Topic = db.Topics.Where(t => t.Id == m.TopicId).Select(t => new TopicDto(t.Id, t.Slug, t.Name, t.Description, t.Concepts.Count)).First() })
            .FirstAsync(ct);
        var user = await db.MatchPlayers.Where(p => p.MatchId == sourceMatchId && p.Seat == seat).Select(p => p.User!).FirstAsync(ct);
        return new GhostOfferDto(UserRefDto.From(user), source.FinishedAt!.Value, source.Topic);
    }

    private static AppException NoGhostOffer() => new(ErrorCodes.QueueNoGhostOffer, "There is no ghost duel on offer.");

    private static QueueCandidate Candidate(QueueEntry q) => new(q.UserId, q.Rating, q.TopicIds, q.EnteredAt);

    private async Task<List<Guid>> LastRankedOpponentsAsync(Guid userId, CancellationToken ct)
    {
        var lastTwo = db.MatchPlayers
            .Where(p => p.UserId == userId && db.Matches.Any(m => m.Id == p.MatchId && m.Kind == MatchKind.Ranked))
            .OrderByDescending(p => p.MatchId)
            .Take(2)
            .Select(p => p.MatchId);
        return await db.MatchPlayers.Where(p => lastTwo.Contains(p.MatchId) && p.UserId != userId && p.UserId != null)
            .Select(p => p.UserId!.Value).ToListAsync(ct);
    }

    private static AppException AlreadyIn() => new(ErrorCodes.QueueAlreadyIn, "You are already in the queue.");
}
