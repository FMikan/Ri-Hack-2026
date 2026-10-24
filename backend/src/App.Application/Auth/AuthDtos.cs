using System.ComponentModel.DataAnnotations;
using App.Application.Me;
using App.Domain;

namespace App.Application.Auth;

public record RegisterRequest
{
    [Required]
    [EmailAddress]
    [StringLength(254)]
    public required string Email { get; init; }

    [Required]
    [StringLength(GameRules.UsernameMaxLength, MinimumLength = GameRules.UsernameMinLength)]
    [RegularExpression("^[A-Za-z0-9_]+$", ErrorMessage = "Username may contain letters, digits and underscores only.")]
    public required string Username { get; init; }

    [Required]
    [StringLength(GameRules.PasswordMaxLength, MinimumLength = GameRules.PasswordMinLength)]
    public required string Password { get; init; }
}

public record LoginRequest
{
    [Required]
    [EmailAddress]
    public required string Email { get; init; }

    [Required]
    public required string Password { get; init; }
}

public record RefreshRequest
{
    [Required]
    public required string RefreshToken { get; init; }
}

public record ForgotPasswordRequest
{
    [Required]
    [EmailAddress]
    public required string Email { get; init; }
}

public record ResetPasswordRequest
{
    [Required]
    public required string Token { get; init; }

    [Required]
    [StringLength(GameRules.PasswordMaxLength, MinimumLength = GameRules.PasswordMinLength)]
    public required string NewPassword { get; init; }
}

public record AuthResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, MeDto User);
