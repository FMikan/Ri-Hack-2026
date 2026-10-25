using App.Domain.Users;

namespace App.Application.Auth;

public interface ITokenService
{
    (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(User user);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string hash, string password);
}

// Checks a "Sign in with Google" ID token: Google's signature, our client id as the audience, not expired.
// Null when the token is not valid, or when Google sign-in is not configured (ClientId is then null too).
public interface IGoogleTokenVerifier
{
    string? ClientId { get; }
    Task<GoogleIdentity?> VerifyAsync(string idToken, CancellationToken cancellationToken);
}

public record GoogleIdentity(string Subject, string Email, bool EmailVerified);

// Sends the "forgot password" link. Returns quickly: slow delivery must not reveal that the e-mail has an account.
public interface IEmailSender
{
    Task SendPasswordResetAsync(string to, string token, CancellationToken cancellationToken);
}
