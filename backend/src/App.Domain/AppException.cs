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
    public const string AuthGoogleInvalid = "urn:app:auth/google-invalid";
    public const string MatchNotFound = "urn:app:match/not-found";
    public const string MatchNotParticipant = "urn:app:match/not-participant";
    public const string MatchAlreadyInMatch = "urn:app:match/already-in-match";
    public const string MatchWrongPhase = "urn:app:match/wrong-phase";
    public const string MatchWrongIndex = "urn:app:match/wrong-index";
    public const string MatchAlreadyAnswered = "urn:app:match/already-answered";
    public const string MatchNotFinished = "urn:app:match/not-finished";
    public const string MatchNotYourTurn = "urn:app:match/not-your-turn";
    public const string MatchTopicNotInPool = "urn:app:match/topic-not-in-pool";
    public const string MatchRematchUnavailable = "urn:app:match/rematch-unavailable";
    public const string QueueNoGhostOffer = "urn:app:queue/no-ghost-offer";
    public const string ShopInsufficientCoins = "urn:app:shop/insufficient-coins";
    public const string ShopAlreadyOwned = "urn:app:shop/already-owned";
    public const string ShopFreezeLimit = "urn:app:shop/freeze-limit";
    public const string ShopNotOwned = "urn:app:shop/not-owned";
    public const string ShopItemNotFound = "urn:app:shop/not-found";
    public const string DailyAlreadyAttempted = "urn:app:daily/already-attempted";
    public const string DailyTooLate = "urn:app:daily/too-late";
    public const string DailyNotFound = "urn:app:daily/not-found";
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
    public const string UserNotFound = "urn:app:users/not-found";
    public const string ProfilePrivate = "urn:app:users/profile-private";
    public const string FriendsAlready = "urn:app:friends/already";
    public const string FriendsSelf = "urn:app:friends/self";
    public const string FriendsNotFriends = "urn:app:friends/not-friends";
    public const string FriendRequestNotFound = "urn:app:friends/request-not-found";
    public const string ChallengeNotFound = "urn:app:challenge/not-found";
    public const string ChallengeExpired = "urn:app:challenge/expired";
    public const string ChallengeOwn = "urn:app:challenge/own";
    public const string LeaderboardNotFound = "urn:app:leaderboards/not-found";
    public const string ProfessorUnavailable = "urn:app:professor/unavailable";
    public const string ProfessorFailed = "urn:app:professor/failed";
    public const string ProfessorDeclined = "urn:app:professor/declined";
    public const string ProfessorChatNotFound = "urn:app:professor/chat-not-found";
    public const string ProfessorMessageNotFound = "urn:app:professor/message-not-found";
    public const string ProfessorNoTopic = "urn:app:professor/no-topic";
    public const string ProfessorLessonNotFound = "urn:app:professor/lesson-not-found";
    public const string ProfessorQuizNotFound = "urn:app:professor/quiz-not-found";
    public const string ProfessorWrongIndex = "urn:app:professor/wrong-index";
    public const string ProfessorQuota = "urn:app:professor/quota";
    public const string PaymentDeclined = "urn:app:payment/declined";
    public const string SubscriptionNotActive = "urn:app:subscription/not-active";
    public const string SubscriptionSeniorOnly = "urn:app:subscription/senior-only";
}
