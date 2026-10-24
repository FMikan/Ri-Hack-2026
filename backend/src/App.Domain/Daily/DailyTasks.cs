namespace App.Domain.Daily;

public record DailyTaskRule(string Key, int Target);

// Three tasks a day from a rotating list (idea §13), 20 coins each and 30 more for all three.
public static class DailyTasks
{
    public const string PlayRanked = "play_2_ranked", PatchConcept = "patch_1_concept", Challenge = "daily_challenge",
        PracticeWeakest = "practice_weakest", Brilliant = "3_brilliant", ReviewAllDue = "review_all_due";
    public const int Count = 3, Reward = 20, Bonus = 30;

    // The same three for a player all day (seeded by day and player). "Review all due" only when something is due
    // (its target is that number), "practice the weakest topic" only when some topic has answers.
    public static List<DailyTaskRule> ForDay(DateOnly day, Guid userId, int dueCount, Guid? weakestTopicId)
    {
        List<DailyTaskRule> options = [new(PlayRanked, 2), new(PatchConcept, 1), new(Challenge, 1), new(Brilliant, 3)];
        if (weakestTopicId is not null) options.Add(new(PracticeWeakest, 1));
        if (dueCount > 0) options.Add(new(ReviewAllDue, dueCount));
        var pool = options.ToArray();
        new Random(HashCode.Combine(day.DayNumber, BitConverter.ToInt32(userId.ToByteArray()))).Shuffle(pool);
        return [.. pool.Take(Count)];
    }
}

// The player's tasks for one day; progress is computed from that day's activity, completion is written once and paid.
public class UserDailyTask
{
    public required Guid UserId { get; init; }
    public required DateOnly Day { get; init; }
    public required string TaskKey { get; init; }
    public required int Target { get; init; }
    public Guid? TopicId { get; init; } // practice_weakest: the weakest topic when the tasks were generated
    public DateTimeOffset? CompletedAt { get; set; }
}

// One find-the-bug question a day, the same for everyone (idea §12).
public class DailyChallenge
{
    public required DateOnly Day { get; init; }
    public required Guid QuestionId { get; init; }
}

// The first GET of the day starts the attempt; 60 s (+1.5 s tolerance) to answer, one attempt.
public class DailyChallengeAttempt
{
    public required Guid UserId { get; init; }
    public required DateOnly Day { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? AnsweredAt { get; set; }
    public string? Payload { get; set; } // jsonb; null = ran out of time
    public bool? IsCorrect { get; set; }
    public double? Fraction { get; set; }
    public int? ElapsedMs { get; set; }
}
