namespace App.Domain;

// "Day" (streak, daily tasks, daily caps, daily challenge) and "week" (Monday 00:00) are local to GameRules.DayTimeZone.
public static class Clock
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById(GameRules.DayTimeZone);

    public static DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, Zone).DateTime);

    // The instant a local day begins (midnight in GameRules.DayTimeZone), for "today" and "this week" queries.
    public static DateTimeOffset StartOf(DateOnly day) =>
        new(TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue), Zone), TimeSpan.Zero);

    public static DateOnly WeekStart(DateTimeOffset now)
    {
        var today = Today(now);
        return today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
    }
}
