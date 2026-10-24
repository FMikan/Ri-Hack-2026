using System.Security.Cryptography;

namespace App.Domain.Social;

public enum FriendshipStatus { Pending, Accepted }

// One row per pair (docs §3.9), the lower id first, so a request in either direction lands on the same row.
public class Friendship
{
    public required Guid UserLow { get; init; }
    public required Guid UserHigh { get; init; }
    public required Guid RequestedBy { get; init; }
    public FriendshipStatus Status { get; set; } = FriendshipStatus.Pending;
    public required DateTimeOffset CreatedAt { get; init; }

    // Guid.CompareTo follows the text form, which is how PostgreSQL orders uuids (the CHECK user_low < user_high).
    public static (Guid Low, Guid High) Pair(Guid a, Guid b) => a.CompareTo(b) < 0 ? (a, b) : (b, a);

    public Guid Other(Guid me) => me == UserLow ? UserHigh : UserLow;
}

// A room code for an unranked duel with the veto (idea §12): open to whoever has the code, or shown in one friend's
// inbox. Valid for 10 minutes; the first guest starts the match.
public class FriendChallenge
{
    public const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ23456789";

    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Code { get; init; }
    public required Guid HostUserId { get; init; }
    public Guid? TargetUserId { get; init; }
    public Guid? GuestUserId { get; set; }
    public Guid? MatchId { get; set; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }

    public static string NewCode() => new(RandomNumberGenerator.GetItems<char>(Alphabet, 6));
}
