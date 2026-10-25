using App.Domain.Content;

namespace App.Domain;

// Every number from the game idea lives here (docs/tehnicki-dokument.md §4.1). Constants arrive with the phase that needs them.
public static class GameRules
{
    public const string DayTimeZone = "Europe/Zagreb";
    public const int StartRating = 1000;
    public const int ProvisionalMatches = 10; // rating shows as "Placement" and stays off leaderboards until then

    // Ranked play (idea §4, §5, §9, §14)
    public const int ProfileAfterRankedMatches = 5; // targeted slots in ranked once both players have this many
    public const int HighKMatches = 30, KHigh = 32, KLow = 16;
    public const int MaxRatingGap = 400, QueueInitialRange = 100, QueueRangeStep = 50, QueueStepSeconds = 10;
    public const int ProposalAcceptSeconds = 10, BotOfferAfterSeconds = 20, GhostOfferAfterSeconds = 30, GhostRecordingMaxDays = 30;
    public const int RematchSeconds = 20, RematchOpponentSeenMinutes = 2;
    public const int VetoAnimationSeconds = 3, VetoBanSeconds = 8, VetoPoolMax = 6;
    public const int TierHysteresis = 25;
    public static readonly int[] TierThresholds = [1100, 1300, 1500, 1700, 1900]; // silver, gold, platinum, diamond, master
    public const int MinTopics = 3, MaxTopics = 5;
    public const int UsernameMinLength = 3, UsernameMaxLength = 20;
    public const int PasswordMinLength = 8, PasswordMaxLength = 128;
    public const int MaxStreakFreezes = 2;

    // Social and leaderboards (idea §12, §14)
    public const int FriendChallengeMinutes = 10, FriendInviteSeconds = 60, OnlineMinutes = 2, LastSeenThrottleSeconds = 30; // invite = sent to one friend
    public const int LeaderboardSize = 100, LeaderboardAround = 2, GrandmasterTop = 100;
    public const int SeasonMonths = 3, SeasonLearningBoostDays = 14, WeeklyTitleTop = 10;
    public static readonly int[] SeasonTitleRanks = [5, 50, 100];

    // Economy (idea §9, §13, §15). Coins, XP and SP only move through the ledger (Rewarder).
    public const int CoinsWin = 15, CoinsLoss = 5, CoinsDraw = 10, CoinsFriendMatch = 5, CoinsLessonPassed = 50, CoinsWeeklyXpGoal = 100, CoinsPerLevel = 50;
    public const int SpWin = 10, SpUpset = 5, SpUpsetGap = 100, SpDailyWinCap = 100, SpPatched = 2, SpDailyLearnCap = 10;
    public const int XpAnswer = 5, XpMatch = 50, XpLesson = 100, WeeklyXpGoal = 500;
    public const int PatchRewardCooldownDays = 7;
    public static readonly (int Days, int Coins)[] StreakMilestones = [(7, 100), (30, 500), (100, 2000)];
    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);
    public static readonly TimeSpan PasswordResetTokenLifetime = TimeSpan.FromHours(1);

    // Match flow (idea §6, §17)
    public const int QuestionsPerMatch = 10;
    public const int LobbySeconds = 5, InterstitialSeconds = 2;
    public const int ClientTimeToleranceMs = 1500; // client-measured time is trusted within this window of the server's
    public const int BotMinSlackMs = 500; // a bot always answers at least this long before the time limit
    public const int ReconnectGraceSeconds = 20; // a human not seen for this long counts as disconnected
    public const int SeenQuestionDays = 30;
    public const int TargetedRotationMatches = 2; // a concept targeted in the last N matches is not targeted again until patched

    // Learning (idea §8, §11)
    public const double MasteryStart = 50, MasteryAlphaMin = 0.15, BlunderWeight = 1.2, UntimedWeight = 0.5;
    public const double WeakMastery = 40, SolidMastery = 70, GraduationMastery = 85, TargetedMaxGap = 20;
    public static readonly double[] DifficultyWeight = [0, 0.7, 0.9, 1.1, 1.3]; // index = D
    // Review steps 1–5 (idea §11); a D1 question halves the interval.
    public static readonly TimeSpan[] ReviewIntervals = [TimeSpan.FromDays(1), TimeSpan.FromDays(3), TimeSpan.FromDays(7), TimeSpan.FromDays(14), TimeSpan.FromDays(30)];

    // Lessons (idea §10) and calibration (idea §11)
    public const int LessonMissedConcepts = 3, LessonChronicConcepts = 2, LessonOtherChecks = 2;
    public static readonly TimeSpan LessonRetryAfter = TimeSpan.FromMinutes(10);
    public const int CalibrationQuestions = 5;
    public const int ReportsToQuarantine = 3; // distinct players
    public const int CalibrationMinAnswers = 50;
    public const double CalibrationEasyRatio = 0.85, CalibrationHardRatio = 0.30;

    // Difficulty band by rating (idea §3): D1 < 1100, D2 < 1400, D3 < 1700, D4 1700+.
    public static readonly int[] DifficultyThresholds = [1100, 1400, 1700];

    public static readonly IReadOnlyDictionary<QuestionType, QuestionTypeRule> Types = new Dictionary<QuestionType, QuestionTypeRule>
    {
        [QuestionType.TrueFalse] = new(TimeMs: 10_000, BasePoints: 50, Guessability: 0.5, MinPerDeck: 0, MaxPerDeck: 2),
        [QuestionType.MultipleChoice] = new(18_000, 100, 0.75, 0, 3),
        [QuestionType.Cloze] = new(22_000, 120, 0.75, 0, 2),
        [QuestionType.OddOneOut] = new(14_000, 100, 0.75, 0, 2),
        [QuestionType.Output] = new(30_000, 150, 0.75, 1, 2),
        [QuestionType.MatchPairs] = new(36_000, 150, 1.0, 0, 2),
        [QuestionType.Order] = new(30_000, 150, 1.0, 0, 2),
        [QuestionType.FindBug] = new(48_000, 200, 1.0, 1, 2),
    };

    // Drag-and-drop types count together: at least 1 and at most 2 per deck.
    public static readonly QuestionType[] DragAndDrop = [QuestionType.MatchPairs, QuestionType.Order];
    public const int DragAndDropMin = 1, DragAndDropMax = 2;
}

public record QuestionTypeRule(int TimeMs, int BasePoints, double Guessability, int MinPerDeck, int MaxPerDeck);
