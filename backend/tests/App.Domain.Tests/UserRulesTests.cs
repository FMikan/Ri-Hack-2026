using App.Domain;
using App.Domain.Users;

namespace App.Domain.Tests;

public class LevelTests
{
    [Fact]
    public void New_user_is_level_1_and_level_2_needs_200_xp()
    {
        Assert.Equal(1, Level.Of(0));
        Assert.Equal(1, Level.Of(199));
        Assert.Equal(2, Level.Of(200));
        Assert.Equal(2, Level.Of(499)); // level 3 needs 300 more
        Assert.Equal(3, Level.Of(500));
    }

    [Fact]
    public void Xp_to_next_level_is_reported()
    {
        Assert.Equal(200, Level.XpToNext(0));
        Assert.Equal(50, Level.XpToNext(450));
    }
}

public class ClockTests
{
    [Fact]
    public void Today_uses_zagreb_zone()
    {
        // 23:30 UTC in winter is 00:30 the next day in Zagreb (UTC+1).
        Assert.Equal(new DateOnly(2026, 1, 16), Clock.Today(new DateTimeOffset(2026, 1, 15, 23, 30, 0, TimeSpan.Zero)));
        // Summer time is UTC+2.
        Assert.Equal(new DateOnly(2026, 7, 16), Clock.Today(new DateTimeOffset(2026, 7, 15, 22, 30, 0, TimeSpan.Zero)));
        Assert.Equal(new DateOnly(2026, 7, 15), Clock.Today(new DateTimeOffset(2026, 7, 15, 21, 30, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void Week_starts_monday()
    {
        // Sunday 25 Oct 2026 → Monday 19 Oct; Monday 26 Oct → itself.
        Assert.Equal(new DateOnly(2026, 10, 19), Clock.WeekStart(new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero)));
        Assert.Equal(new DateOnly(2026, 10, 26), Clock.WeekStart(new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero)));
    }
}
