namespace App.Domain.Users;

// A "forgot password" link: single use, valid for an hour, spent together with every older link once one is used.
public class PasswordResetToken
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserId { get; init; }
    public required string TokenHash { get; init; } // SHA-256 of the token; the token itself only travels in the e-mail
    public required DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? UsedAt { get; set; }
    public required DateTimeOffset CreatedAt { get; init; }
}
