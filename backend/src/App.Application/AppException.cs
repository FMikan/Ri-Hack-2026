namespace App.Application;

// A game-rule or auth violation. The API turns it into RFC 9457 Problem Details with type "urn:app:<code>" (docs §6).
public class AppException(string code, string detail, int status = 409) : Exception(detail)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}

public static class ErrorCodes
{
    public const string TopicsCount = "urn:app:topics/count";
    public const string TopicsLockedInQueue = "urn:app:topics/locked-in-queue";
    public const string TopicNotFound = "urn:app:topics/not-found";
    public const string AuthInvalidCredentials = "urn:app:auth/invalid-credentials";
    public const string AuthEmailTaken = "urn:app:auth/email-taken";
    public const string AuthUsernameTaken = "urn:app:auth/username-taken";
    public const string AuthRefreshInvalid = "urn:app:auth/refresh-invalid";
    public const string AuthResetTokenInvalid = "urn:app:auth/reset-token-invalid";
}
