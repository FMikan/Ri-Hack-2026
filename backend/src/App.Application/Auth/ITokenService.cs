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
