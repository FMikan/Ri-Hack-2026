using System.Security.Cryptography;
using System.Text;
using App.Application.Me;
using App.Domain;
using App.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace App.Application.Auth;

public class AuthService(IAppDbContext db, IPasswordHasher hasher, ITokenService tokens, MeService me, TimeProvider time)
{
    // Verified against when the e-mail is unknown, so a wrong e-mail takes as long as a wrong password.
    private static string? _dummyHash;

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var username = request.Username.Trim();
        await EnsureAvailableAsync(email, username, cancellationToken);

        var user = new User
        {
            Email = email,
            Username = username,
            UsernameNormalized = User.Normalize(username),
            PasswordHash = hasher.Hash(request.Password),
            CreatedAt = time.GetUtcNow(),
        };
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent registration won the unique index: report it as the same 409 the pre-check gives.
            await EnsureAvailableAsync(email, username, cancellationToken);
            throw;
        }

        return await IssueAsync(user, Guid.CreateVersion7(), cancellationToken);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.Include(u => u.Topics).FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        _dummyHash ??= hasher.Hash("not-a-real-password");
        var valid = hasher.Verify(user?.PasswordHash ?? _dummyHash, request.Password);
        if (user is null || !valid)
            throw new AppException(ErrorCodes.AuthInvalidCredentials, "E-mail or password is incorrect.", 401);

        return await IssueAsync(user, Guid.CreateVersion7(), cancellationToken);
    }

    // Rotation: the presented token is revoked and replaced. A token that is already revoked means it leaked
    // (or the client raced itself), so the whole family is revoked and the user must log in again.
    public async Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var hash = Hash(refreshToken);
        var token = await db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken)
            ?? throw RefreshInvalid();

        var nextId = Guid.CreateVersion7();
        var claimed = token.ExpiresAt > now && await db.RefreshTokens
            .Where(t => t.Id == token.Id && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now).SetProperty(t => t.ReplacedBy, nextId), cancellationToken) == 1;
        if (!claimed)
        {
            await RevokeFamilyAsync(token.FamilyId, now, cancellationToken);
            throw RefreshInvalid();
        }

        var user = await db.Users.Include(u => u.Topics).FirstAsync(u => u.Id == token.UserId, cancellationToken);
        return await IssueAsync(user, token.FamilyId, cancellationToken, nextId);
    }

    public Task LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var hash = Hash(refreshToken);
        return db.RefreshTokens
            .Where(t => t.TokenHash == hash && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);
    }

    private async Task<AuthResponse> IssueAsync(User user, Guid familyId, CancellationToken cancellationToken, Guid? refreshTokenId = null)
    {
        var now = time.GetUtcNow();
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = refreshTokenId ?? Guid.CreateVersion7(),
            UserId = user.Id,
            TokenHash = Hash(refreshToken),
            FamilyId = familyId,
            ExpiresAt = now + GameRules.RefreshTokenLifetime,
            CreatedAt = now,
        });
        await db.SaveChangesAsync(cancellationToken);

        var (accessToken, expiresAt) = tokens.CreateAccessToken(user);
        return new AuthResponse(accessToken, expiresAt, refreshToken, me.ToDto(user));
    }

    private Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);

    private async Task EnsureAvailableAsync(string email, string username, CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
            throw new AppException(ErrorCodes.AuthEmailTaken, "An account with this e-mail already exists.");
        var normalized = User.Normalize(username);
        if (await db.Users.AnyAsync(u => u.UsernameNormalized == normalized, cancellationToken))
            throw new AppException(ErrorCodes.AuthUsernameTaken, "This username is taken.");
    }

    private static AppException RefreshInvalid() =>
        new(ErrorCodes.AuthRefreshInvalid, "The session has expired. Log in again.", 401);

    private static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
