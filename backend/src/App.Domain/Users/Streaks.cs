namespace App.Domain.Users;

public static class Streaks
{
    // One activity per day counts (idea §13): the next day adds one, missed days use frozen days when there are
    // enough of them, otherwise the streak starts again. Returns the milestone (7, 30, 100) the streak just reached.
    public static int? Apply(User user, DateOnly today)
    {
        if (user.StreakLastActiveOn == today) return null;
        var missed = user.StreakLastActiveOn is { } last ? today.DayNumber - last.DayNumber - 1 : int.MaxValue;
        if (missed <= user.StreakFreezes)
        {
            user.StreakFreezes -= missed;
            user.StreakCount++;
        }
        else user.StreakCount = 1;
        user.StreakLastActiveOn = today;
        return GameRules.StreakMilestones.Any(m => m.Days == user.StreakCount) ? user.StreakCount : null;
    }

    // What to show: a streak whose gap the frozen days cannot cover any more is already 0.
    public static int Current(User user, DateOnly today) =>
        user.StreakLastActiveOn is { } last && today.DayNumber - last.DayNumber - 1 <= user.StreakFreezes ? user.StreakCount : 0;
}
