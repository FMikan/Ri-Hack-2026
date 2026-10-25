using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using App.Application.Me;
using App.Domain;
using App.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace App.Application.Auth;

public class AuthService(
    IAppDbContext db, IPasswordHasher hasher, ITokenService tokens, IGoogleTokenVerifier google, IEmailSender emails, MeService me, TimeProvider time)
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

    public AuthConfigDto Config() => new(google.ClientId);

    // Sign in with Google. The same Google account always opens the same user; otherwise a verified e-mail that has an
    // account without Google yet is linked to it; anyone else gets a new user with a free username and no password.
    public async Task<AuthResponse> GoogleLoginAsync(string idToken, CancellationToken cancellationToken)
    {
        var identity = await google.VerifyAsync(idToken, cancellationToken);
        // An unverified address may belong to someone else's account, so it never signs in.
        if (identity is not { EmailVerified: true })
            throw new AppException(ErrorCodes.AuthGoogleInvalid, "Google sign-in did not work. Try again.", 401);

        var email = identity.Email.Trim().ToLowerInvariant();
        var user = await db.Users.Include(u => u.Topics).FirstOrDefaultAsync(u => u.GoogleSubject == identity.Subject, cancellationToken)
            ?? await db.Users.Include(u => u.Topics).FirstOrDefaultAsync(u => u.Email == email && u.GoogleSubject == null, cancellationToken);
        if (user is not null)
        {
            user.GoogleSubject = identity.Subject; // saved with the refresh token in IssueAsync
            return await IssueAsync(user, Guid.CreateVersion7(), cancellationToken);
        }

        var username = await FreeUsernameAsync(email, cancellationToken);
        // The e-mail can still be taken by an account that is linked to another Google account: the same 409 as register.
        await EnsureAvailableAsync(email, username, cancellationToken);
        user = new User
        {
            Email = email,
            Username = username,
            UsernameNormalized = User.Normalize(username),
            GoogleSubject = identity.Subject,
            CreatedAt = time.GetUtcNow(),
        };
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await EnsureAvailableAsync(email, username, cancellationToken);
            throw;
        }
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

    // Answers the same for every e-mail, so the endpoint never tells whether an account exists.
    public async Task ForgotPasswordAsync(string email, CancellationToken cancellationToken)
    {
        email = email.Trim().ToLowerInvariant();
        var userId = await db.Users.Where(u => u.Email == email).Select(u => (Guid?)u.Id).FirstOrDefaultAsync(cancellationToken);
        if (userId is null) return;

        var now = time.GetUtcNow();
        var token = NewToken();
        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = userId.Value,
            TokenHash = Hash(token),
            ExpiresAt = now + GameRules.PasswordResetTokenLifetime,
            CreatedAt = now,
        });
        await db.SaveChangesAsync(cancellationToken);
        await emails.SendPasswordResetAsync(email, token, cancellationToken);
    }

    // Spends the link (and every older one), sets the password and logs the user out everywhere.
    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var hash = Hash(request.Token);
        var token = await db.PasswordResetTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // Claimed with a conditional update, so two requests with the same link cannot both succeed.
        var claimed = token is not null && token.ExpiresAt > now && await db.PasswordResetTokens
            .Where(t => t.Id == token.Id && t.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, now), cancellationToken) == 1;
        if (!claimed)
            throw new AppException(ErrorCodes.AuthResetTokenInvalid, "This reset link is invalid or has expired. Request a new one.");

        var userId = token!.UserId;
        var passwordHash = hasher.Hash(request.NewPassword);
        await db.PasswordResetTokens
            .Where(t => t.UserId == userId && t.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, now), cancellationToken);
        await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(s => s.SetProperty(u => u.PasswordHash, passwordHash), cancellationToken);
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<AuthResponse> IssueAsync(User user, Guid familyId, CancellationToken cancellationToken, Guid? refreshTokenId = null)
    {
        var now = time.GetUtcNow();
        var refreshToken = NewToken();
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
        return new AuthResponse(accessToken, expiresAt, refreshToken, await me.ToDtoAsync(user, cancellationToken));
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

    // From the e-mail: "Ana.K+games@gmail.com" → "anak". Too short becomes "player"; a taken name gets "_1234".
    private async Task<string> FreeUsernameAsync(string email, CancellationToken cancellationToken)
    {
        var local = email.Split('@')[0].Split('+')[0];
        var stem = new string([.. local.Where(c => char.IsAsciiLetterOrDigit(c) || c == '_')]).ToLowerInvariant();
        if (stem.Length < GameRules.UsernameMinLength) stem = "player";
        stem = stem[..Math.Min(stem.Length, GameRules.UsernameMaxLength - 5)]; // room for "_1234"
        var username = stem;
        while (await db.Users.AnyAsync(u => u.UsernameNormalized == username, cancellationToken))
            username = $"{stem}_{Random.Shared.Next(1000, 10000)}";
        return username;
    }

    private static AppException RefreshInvalid() =>
        new(ErrorCodes.AuthRefreshInvalid, "The session has expired. Log in again.", 401);

    // 32 random bytes, URL-safe, so it also fits in the reset link's query string.
    private static string NewToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    private static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
