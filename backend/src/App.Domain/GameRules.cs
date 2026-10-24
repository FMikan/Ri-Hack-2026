using App.Domain.Content;

namespace App.Domain;

// Every number from the game idea lives here (docs/tehnicki-dokument.md §4.1). Constants arrive with the phase that needs them.
public static class GameRules
{
    public const string DayTimeZone = "Europe/Zagreb";
    public const int StartRating = 1000;
    public const int ProvisionalMatches = 10; // rating shows as "?" and stays off leaderboards until then
    public const int MinTopics = 3, MaxTopics = 5;
    public const int UsernameMinLength = 3, UsernameMaxLength = 20;
    public const int PasswordMinLength = 8, PasswordMaxLength = 128;
    public const int MaxStreakFreezes = 2;
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

    // Difficulty band by rating (idea §3): D1 < 1100, D2 < 1400, D3 < 1700, D4 1700+.
    public static readonly int[] DifficultyThresholds = [1100, 1400, 1700];

    public static readonly IReadOnlyDictionary<QuestionType, QuestionTypeRule> Types = new Dictionary<QuestionType, QuestionTypeRule>
    {
        [QuestionType.TrueFalse] = new(TimeMs: 8_000, BasePoints: 50, Guessability: 0.5, MinPerDeck: 0, MaxPerDeck: 2),
        [QuestionType.MultipleChoice] = new(15_000, 100, 0.75, 0, 3),
        [QuestionType.Cloze] = new(18_000, 120, 0.75, 0, 2),
        [QuestionType.OddOneOut] = new(12_000, 100, 0.75, 0, 2),
        [QuestionType.Output] = new(25_000, 150, 0.75, 1, 2),
        [QuestionType.MatchPairs] = new(30_000, 150, 1.0, 0, 2),
        [QuestionType.Order] = new(25_000, 150, 1.0, 0, 2),
        [QuestionType.FindBug] = new(40_000, 200, 1.0, 1, 2),
    };

    // Drag-and-drop types count together: at least 1 and at most 2 per deck.
    public static readonly QuestionType[] DragAndDrop = [QuestionType.MatchPairs, QuestionType.Order];
    public const int DragAndDropMin = 1, DragAndDropMax = 2;
}

public record QuestionTypeRule(int TimeMs, int BasePoints, double Guessability, int MinPerDeck, int MaxPerDeck);
