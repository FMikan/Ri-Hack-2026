namespace App.Domain;

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
    public const string TopicNotPlayable = "urn:app:topics/not-playable";
    public const string AuthInvalidCredentials = "urn:app:auth/invalid-credentials";
    public const string AuthEmailTaken = "urn:app:auth/email-taken";
    public const string AuthUsernameTaken = "urn:app:auth/username-taken";
    public const string AuthRefreshInvalid = "urn:app:auth/refresh-invalid";
    public const string AuthResetTokenInvalid = "urn:app:auth/reset-token-invalid";
    public const string MatchNotFound = "urn:app:match/not-found";
    public const string MatchNotParticipant = "urn:app:match/not-participant";
    public const string MatchAlreadyInMatch = "urn:app:match/already-in-match";
    public const string MatchWrongPhase = "urn:app:match/wrong-phase";
    public const string MatchWrongIndex = "urn:app:match/wrong-index";
    public const string MatchAlreadyAnswered = "urn:app:match/already-answered";
    public const string MatchNotFinished = "urn:app:match/not-finished";
    public const string MatchNotYourTurn = "urn:app:match/not-your-turn";
    public const string MatchTopicNotInPool = "urn:app:match/topic-not-in-pool";
    public const string QueueAlreadyIn = "urn:app:queue/already-in";
    public const string QueueNotIn = "urn:app:queue/not-in";
    public const string ProposalExpired = "urn:app:proposal/expired";
    public const string MatchNothingToCalibrate = "urn:app:match/nothing-to-calibrate";
    public const string QuestionNotFound = "urn:app:question/not-found";
    public const string QuestionNotAnswered = "urn:app:question/not-answered";
    public const string LessonNotFound = "urn:app:lesson/not-found";
    public const string LessonNothingMissed = "urn:app:lesson/nothing-missed";
    public const string LessonAlreadyPassed = "urn:app:lesson/already-passed";
    public const string LessonRetryTooSoon = "urn:app:lesson/retry-too-soon";
}
