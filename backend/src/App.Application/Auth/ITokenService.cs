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

// Sends the "forgot password" link. Returns quickly: slow delivery must not reveal that the e-mail has an account.
public interface IEmailSender
{
    Task SendPasswordResetAsync(string to, string token, CancellationToken cancellationToken);
}
