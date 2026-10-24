namespace App.Domain.Users;

// One row per issued refresh token; rotation links old to new, and a reused revoked token revokes its whole family.
public class RefreshToken
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserId { get; init; }
    public required string TokenHash { get; init; } // SHA-256 of the token; the token itself is never stored
    public required Guid FamilyId { get; init; } // one login = one family
    public required DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedBy { get; set; }
    public required DateTimeOffset CreatedAt { get; init; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
}
