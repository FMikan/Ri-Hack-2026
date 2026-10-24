using System.Security.Claims;
using System.Text;
using App.Application;
using App.Application.Auth;
using App.Domain;
using App.Domain.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace App.Api;

// JWT access tokens in the Authorization header (no cookies, so the same flow works in the Android WebView later).
public static class Auth
{
    private const string Issuer = "ri-hack", Audience = "ri-hack";

    public static IServiceCollection AddAppAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var signingKey = configuration["Auth:JwtSigningKey"];
        if (string.IsNullOrEmpty(signingKey) || signingKey.Length < 32)
            throw new InvalidOperationException(
                "Auth:JwtSigningKey is missing or shorter than 32 characters. Set Auth__JwtSigningKey in the repository-root .env " +
                "(generate one with: openssl rand -hex 32).");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));

        services.AddSingleton<ITokenService>(sp => new JwtTokenService(key, sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IPasswordHasher, PasswordHasherAdapter>();
        services.AddAuthorization();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        // Lifetime is checked against the injected TimeProvider, so tests that move the clock also expire tokens.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<TimeProvider>((options, time) =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = Issuer,
                ValidAudience = Audience,
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.FromSeconds(30),
                LifetimeValidator = (notBefore, expires, _, parameters) =>
                {
                    var now = time.GetUtcNow().UtcDateTime;
                    return (notBefore is null || notBefore <= now + parameters.ClockSkew)
                        && expires is not null && expires > now - parameters.ClockSkew;
                },
            };
            // Online status (friends): every authenticated request marks the user as seen, at most once in 30 s.
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var userId = context.Principal!.UserId();
                    var now = time.GetUtcNow();
                    var stale = now.AddSeconds(-GameRules.LastSeenThrottleSeconds);
                    await context.HttpContext.RequestServices.GetRequiredService<IAppDbContext>().Users
                        .Where(u => u.Id == userId && (u.LastSeenAt == null || u.LastSeenAt < stale))
                        .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastSeenAt, now), context.HttpContext.RequestAborted);
                },
            };
        });
        return services;
    }

    public static Guid UserId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? throw new InvalidOperationException("Endpoint is missing RequireAuthorization()."));

    private sealed class JwtTokenService(SecurityKey key, TimeProvider time) : ITokenService
    {
        private readonly JsonWebTokenHandler _handler = new();

        public (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(User user)
        {
            var now = time.GetUtcNow();
            var expiresAt = now + GameRules.AccessTokenLifetime;
            var token = _handler.CreateToken(new SecurityTokenDescriptor
            {
                Issuer = Issuer,
                Audience = Audience,
                Claims = new Dictionary<string, object> { [JwtRegisteredClaimNames.Sub] = user.Id.ToString() },
                IssuedAt = now.UtcDateTime,
                NotBefore = now.UtcDateTime,
                Expires = expiresAt.UtcDateTime,
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
            });
            return (token, expiresAt);
        }
    }

    private sealed class PasswordHasherAdapter : IPasswordHasher
    {
        private readonly PasswordHasher<User> _hasher = new();

        public string Hash(string password) => _hasher.HashPassword(null!, password);

        public bool Verify(string hash, string password) =>
            _hasher.VerifyHashedPassword(null!, hash, password) != PasswordVerificationResult.Failed;
    }
}
