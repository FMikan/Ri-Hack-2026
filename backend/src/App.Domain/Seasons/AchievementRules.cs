namespace App.Domain.Seasons;

// Counters an achievement can be measured on; all of them come from the player's history.
public record UserSnapshot(int Wins, int Brilliants, int BestStreak, int ConceptsPatched, int UpsetWins, int TopicsAbove50, int Topics, int DailyChallengeRun);

public record AchievementRule(string Key, string Name, Func<UserSnapshot, (int Progress, int Target)> Measure);

// Permanent records (idea §14). A new one is a line here; the key is what user_achievements stores.
public static class AchievementRules
{
    public static readonly AchievementRule[] All =
    [
        new("first_win", "First win", s => (s.Wins, 1)),
        new("brilliant_10", "10 Brilliant answers", s => (s.Brilliants, 10)),
        new("streak_7", "7-day streak", s => (s.BestStreak, 7)),
        new("streak_30", "30-day streak", s => (s.BestStreak, 30)),
        new("streak_100", "100-day streak", s => (s.BestStreak, 100)),
        new("patched_50", "50 concepts patched", s => (s.ConceptsPatched, 50)),
        new("giant_slayer", "Beat a player rated 200+ higher", s => (s.UpsetWins, 1)),
        new("all_topics_50", "Every topic above 50 mastery", s => (s.TopicsAbove50, s.Topics)),
        new("daily_30", "Daily challenge 30 days in a row", s => (s.DailyChallengeRun, 30)),
    ];

    public const int GiantSlayerGap = 200, TopicMastery = 50;

    public static string[] Evaluate(UserSnapshot snapshot, IReadOnlySet<string> earned) =>
        [.. All.Where(r => !earned.Contains(r.Key) && r.Measure(snapshot) is var (progress, target) && target > 0 && progress >= target).Select(r => r.Key)];
}
