using System.ComponentModel.DataAnnotations;
using App.Application.Matches;
using App.Application.Me;
using App.Application.Queue;
using App.Domain;
using App.Domain.Matches;
using App.Domain.Social;
using App.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace App.Application.Social;

// Mirrors docs/tehnicki-dokument.md §6.9. Keep frontend/src/types/api.ts in sync.

public enum FriendStatus { PendingIn, Accepted, PendingOut } // also the order of the friends list

public record FriendDto(UserRefDto User, FriendStatus Status, bool Online);

public record FriendRequest
{
    [Required]
    [StringLength(GameRules.UsernameMaxLength, MinimumLength = 1)]
    public required string Username { get; init; }
}

public record CreateFriendChallengeRequest
{
    public Guid? FriendUserId { get; init; } // shown in that friend's dashboard inbox; without it the code is open to anyone
}

public record FriendChallengeCreatedDto(string Code, DateTimeOffset ExpiresAt);

public record FriendChallengeDto(string Code, UserRefDto Host, Guid? MatchId, DateTimeOffset ExpiresAt);

// Friends (idea §12): one row per pair, a request either way, online = seen in the last 2 minutes.
// Friend challenges: a code that starts an unranked duel with the veto for the first guest who joins.
public class SocialService(IAppDbContext db, MatchService matches, TimeProvider time)
{
    // Accepted friends of a user, usable inside other queries (leaderboard, profile visibility, challenges).
    public static IQueryable<Guid> FriendIds(IAppDbContext db, Guid userId) =>
        db.Friendships.Where(f => f.Status == FriendshipStatus.Accepted && (f.UserLow == userId || f.UserHigh == userId))
            .Select(f => f.UserLow == userId ? f.UserHigh : f.UserLow);

    // Requests to me first, then friends (online first), then my own open requests.
    public async Task<List<FriendDto>> ListAsync(Guid userId, CancellationToken ct)
    {
        var rows = await db.Friendships.AsNoTracking().Where(f => f.UserLow == userId || f.UserHigh == userId).ToListAsync(ct);
        var ids = rows.Select(f => f.Other(userId)).ToList();
        var users = await db.Users.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);
        return rows.Select(f => Dto(f, userId, users[f.Other(userId)]))
            .OrderBy(f => f.Status).ThenByDescending(f => f.Online).ThenBy(f => f.User.Username, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // Asking someone who already asked me accepts their request: either way it is one row.
    public async Task<FriendDto> RequestAsync(Guid userId, string username, CancellationToken ct)
    {
        var normalized = User.Normalize(username.Trim());
        var other = await db.Users.FirstOrDefaultAsync(u => u.UsernameNormalized == normalized, ct) ?? throw UserNotFound();
        if (other.Id == userId) throw new AppException(ErrorCodes.FriendsSelf, "You cannot add yourself.");
        var (low, high) = Friendship.Pair(userId, other.Id);
        var inserted = await db.Database.ExecuteSqlAsync(
            $"insert into friendships (user_low, user_high, requested_by, status, created_at) values ({low}, {high}, {userId}, 'pending', {time.GetUtcNow()}) on conflict do nothing", ct);
        if (inserted == 0 && await Pair(low, high).Where(f => f.Status == FriendshipStatus.Pending && f.RequestedBy == other.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.Status, FriendshipStatus.Accepted), ct) == 0)
            throw new AppException(ErrorCodes.FriendsAlready, "You are already friends, or your request is waiting for an answer.");
        return Dto(await Pair(low, high).AsNoTracking().FirstAsync(ct), userId, other);
    }

    public async Task<FriendDto> AcceptAsync(Guid userId, Guid fromUserId, CancellationToken ct)
    {
        var (low, high) = Friendship.Pair(userId, fromUserId);
        if (await Pair(low, high).Where(f => f.RequestedBy == fromUserId || f.Status == FriendshipStatus.Accepted)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.Status, FriendshipStatus.Accepted), ct) == 0)
            throw new AppException(ErrorCodes.FriendRequestNotFound, "There is no friend request from this player.", 404);
        return Dto(await Pair(low, high).AsNoTracking().FirstAsync(ct), userId, await db.Users.FirstAsync(u => u.Id == fromUserId, ct));
    }

    // Unfriends, withdraws my request or declines theirs.
    public Task RemoveAsync(Guid userId, Guid otherUserId, CancellationToken ct)
    {
        var (low, high) = Friendship.Pair(userId, otherUserId);
        return Pair(low, high).ExecuteDeleteAsync(ct);
    }

    public async Task<FriendChallengeCreatedDto> CreateChallengeAsync(Guid userId, Guid? friendUserId, CancellationToken ct)
    {
        await RequireTopicsAsync(userId, ct);
        if (friendUserId is { } friend && !await FriendIds(db, userId).ContainsAsync(friend, ct))
            throw new AppException(ErrorCodes.FriendsNotFriends, "Only friends get a challenge in their inbox; share the code instead.");
        var now = time.GetUtcNow();
        var challenge = new FriendChallenge
        {
            Code = FriendChallenge.NewCode(), HostUserId = userId, TargetUserId = friendUserId,
            ExpiresAt = friendUserId is null ? now.AddMinutes(GameRules.FriendChallengeMinutes) : now.AddSeconds(GameRules.FriendInviteSeconds),
            CreatedAt = now,
        };
        db.FriendChallenges.Add(challenge);
        await db.SaveChangesAsync(ct);
        return new FriendChallengeCreatedDto(challenge.Code, challenge.ExpiresAt);
    }

    // The host polls this to see the match the guest started.
    public async Task<FriendChallengeDto> GetChallengeAsync(string code, CancellationToken ct)
    {
        var normalized = code.ToUpperInvariant();
        var challenge = await db.FriendChallenges.AsNoTracking().FirstOrDefaultAsync(c => c.Code == normalized, ct) ?? throw ChallengeNotFound();
        var host = await db.Users.FirstAsync(u => u.Id == challenge.HostUserId, ct);
        return new FriendChallengeDto(challenge.Code, UserRefDto.From(host), challenge.MatchId, challenge.ExpiresAt);
    }

    // The friend it was sent to says no: it closes at once, so the host's poll sees it expire. Anyone else changes nothing.
    public Task DeclineChallengeAsync(Guid userId, string code, CancellationToken ct)
    {
        var (normalized, now) = (code.ToUpperInvariant(), time.GetUtcNow());
        return db.FriendChallenges.Where(c => c.Code == normalized && c.TargetUserId == userId && c.MatchId == null && c.ExpiresAt > now)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ExpiresAt, now), ct);
    }

    // Under the challenge's row lock, so two guests at once start one match. Both players leave the ranked queue.
    public async Task<MatchStateDto> JoinChallengeAsync(Guid userId, string code, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var normalized = code.ToUpperInvariant();
        Guid matchId;
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            await db.Database.SqlQuery<int>($"select 1 as \"Value\" from friend_challenges where code = {normalized} for update").ToListAsync(ct);
            var challenge = await db.FriendChallenges.FirstOrDefaultAsync(c => c.Code == normalized, ct) ?? throw ChallengeNotFound();
            if (challenge.HostUserId == userId)
                throw new AppException(ErrorCodes.ChallengeOwn, "Send the code to a friend; you cannot join your own challenge.");
            if (challenge.MatchId is not null || challenge.ExpiresAt <= now)
                throw new AppException(ErrorCodes.ChallengeExpired, "This challenge is no longer open.");
            await RequireTopicsAsync(userId, ct);

            await db.LeaveQueueAsync(challenge.HostUserId, ct);
            await db.LeaveQueueAsync(userId, ct);
            matchId = await matches.CreateDuelAsync(challenge.HostUserId, userId, MatchKind.Friend, now, ct);
            (challenge.GuestUserId, challenge.MatchId) = (userId, matchId);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        return await matches.GetStateAsync(userId, matchId, ct);
    }

    // The veto needs a topic pool from both players (idea §5).
    private async Task RequireTopicsAsync(Guid userId, CancellationToken ct)
    {
        if (await db.UserTopics.CountAsync(t => t.UserId == userId, ct) < GameRules.MinTopics)
            throw new AppException(ErrorCodes.TopicsCount, $"Pick {GameRules.MinTopics} to {GameRules.MaxTopics} topics first.");
    }

    private IQueryable<Friendship> Pair(Guid low, Guid high) => db.Friendships.Where(f => f.UserLow == low && f.UserHigh == high);

    private FriendDto Dto(Friendship f, Guid me, User other) => new(
        UserRefDto.From(other),
        f.Status == FriendshipStatus.Accepted ? FriendStatus.Accepted : f.RequestedBy == me ? FriendStatus.PendingOut : FriendStatus.PendingIn,
        other.LastSeenAt >= time.GetUtcNow().AddMinutes(-GameRules.OnlineMinutes));

    public static AppException UserNotFound() => new(ErrorCodes.UserNotFound, "There is no player with this username.", 404);

    private static AppException ChallengeNotFound() => new(ErrorCodes.ChallengeNotFound, "There is no challenge with this code.", 404);
}
