using App.Domain.Economy;
using App.Domain.Matches;
using App.Domain.Progress;
using App.Domain.Users;
using static App.Domain.Tests.Fixtures;

namespace App.Domain.Tests;

public class MatchRewardTests
{
    private static Match Finished(MatchKind kind, int? winnerSeat, int myRating = 1000, int opponentRating = 1000, MatchResultReason reason = MatchResultReason.Score, int answered = 10)
    {
        var match = new Match { Kind = kind, RngSeed = 1, CreatedAt = Now, Phase = MatchPhase.Done, WinnerSeat = winnerSeat, ResultReason = reason };
        match.Players.Add(new MatchPlayer { Seat = 0, Kind = SeatKind.Human, UserId = Guid.NewGuid(), DisplayRating = myRating });
        match.Players.Add(new MatchPlayer { Seat = 1, Kind = kind == MatchKind.Bot ? SeatKind.Bot : SeatKind.Human, UserId = Guid.NewGuid(), DisplayRating = opponentRating });
        for (var i = 0; i < 10; i++)
            match.Answers.Add(new MatchAnswer { Seat = 0, Index = i, IsCorrect = false, Fraction = 0, Points = 0, ElapsedMs = 1, TimedOut = i >= answered, AnsweredAt = Now, Label = AnswerLabel.Miss });
        return match;
    }

    private static (int Coins, int Xp, int Sp) Sum(IEnumerable<Grant> grants) =>
        grants.Aggregate((0, 0, 0), (s, g) => (s.Item1 + g.Coins, s.Item2 + g.Xp, s.Item3 + g.Sp));

    [Fact]
    public void Ranked_win_gives_15_coins_10_sp_and_xp_for_the_match_and_every_answer()
    {
        var match = Finished(MatchKind.Ranked, winnerSeat: 0, answered: 8);

        var grants = Rewards.ForMatch(match, match.Players[0], spFromWinsToday: 0);

        Assert.Equal((15, 50 + 8 * 5, 10), Sum(grants));
        Assert.Contains(grants, g => g.Type == RewardType.MatchWin && g.DedupeKey == $"match_win:{match.Id}:{match.Players[0].UserId}");
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(null, 10)]
    public void Ranked_loss_and_draw_give_5_and_10_coins_and_no_sp(int? winner, int coins)
    {
        var match = Finished(MatchKind.Ranked, winner);

        Assert.Equal((coins, 100, 0), Sum(Rewards.ForMatch(match, match.Players[0], 0)));
    }

    [Fact]
    public void Upset_win_against_100_higher_adds_5_sp()
    {
        var match = Finished(MatchKind.Ghost, winnerSeat: 0, myRating: 1000, opponentRating: 1100);

        Assert.Equal(15, Sum(Rewards.ForMatch(match, match.Players[0], 0)).Sp);
        Assert.Contains(Rewards.ForMatch(match, match.Players[0], 0), g => g.Type == RewardType.RatingUpset);
    }

    [Fact]
    public void Sp_from_wins_is_capped_at_100_per_day()
    {
        var match = Finished(MatchKind.Ranked, winnerSeat: 0, opponentRating: 1150);

        Assert.Equal(5, Sum(Rewards.ForMatch(match, match.Players[0], spFromWinsToday: 95)).Sp);
        Assert.Equal(0, Sum(Rewards.ForMatch(match, match.Players[0], spFromWinsToday: 100)).Sp);
    }

    [Fact]
    public void Bot_and_practice_give_xp_only()
    {
        var bot = Finished(MatchKind.Bot, winnerSeat: 0);

        Assert.Equal((0, 100, 0), Sum(Rewards.ForMatch(bot, bot.Players[0], 0)));
    }

    [Fact]
    public void Calibration_and_aborted_matches_give_nothing()
    {
        var calibration = Finished(MatchKind.Calibration, null);
        var aborted = Finished(MatchKind.Ranked, null, reason: MatchResultReason.Aborted);

        Assert.Empty(Rewards.ForMatch(calibration, calibration.Players[0], 0));
        Assert.Empty(Rewards.ForMatch(aborted, aborted.Players[0], 0));
    }
}

public class LessonAndLevelRewardTests
{
    private static readonly Guid User = Guid.NewGuid(), Lesson = Guid.NewGuid();

    [Fact]
    public void Lesson_pass_gives_50_coins_100_xp_and_2_sp_per_eligible_concept()
    {
        var grants = Rewards.ForLesson(User, Lesson, [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()], spFromLearningToday: 0);

        Assert.Equal(50, grants.Sum(g => g.Coins));
        Assert.Equal(100, grants.Sum(g => g.Xp));
        Assert.Equal(6, grants.Sum(g => g.Sp));
        Assert.Equal(3, grants.Count(g => g.Type == RewardType.ConceptPatched));
    }

    [Fact]
    public void Learning_sp_is_capped_at_10_per_day()
    {
        var grants = Rewards.ForLesson(User, Lesson, [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()], spFromLearningToday: 6);

        Assert.Equal(4, grants.Sum(g => g.Sp));
    }

    [Fact]
    public void Lesson_without_an_eligible_concept_gives_nothing() => Assert.Empty(Rewards.ForLesson(User, Lesson, [], 0));

    [Fact]
    public void Level_up_rewards_50_times_each_reached_level()
    {
        var grants = Rewards.LevelUps(User, oldXp: 150, newXp: 520);

        Assert.Equal([2, 3], grants.Select(g => g.Coins / 50));
        Assert.Equal($"level_up:{User}:3", grants[1].DedupeKey);
        Assert.Empty(Rewards.LevelUps(User, 210, 300));
    }
}

public class StreakTests
{
    private static readonly DateOnly Day = new(2026, 10, 24);

    private static User Player(int count, DateOnly? last, int freezes = 0) =>
        new() { Email = "a", Username = "a", UsernameNormalized = "a", PasswordHash = "x", CreatedAt = Now, StreakCount = count, StreakLastActiveOn = last, StreakFreezes = freezes };

    [Fact]
    public void Streak_starts_at_1_and_increments_on_consecutive_day()
    {
        var user = Player(0, null);

        Streaks.Apply(user, Day);
        Streaks.Apply(user, Day); // the same day counts once
        Streaks.Apply(user, Day.AddDays(1));

        Assert.Equal((2, Day.AddDays(1)), (user.StreakCount, user.StreakLastActiveOn));
    }

    [Fact]
    public void Streak_uses_freeze_for_one_missed_day()
    {
        var user = Player(4, Day, freezes: 1);

        Streaks.Apply(user, Day.AddDays(2));

        Assert.Equal((5, 0), (user.StreakCount, user.StreakFreezes));
    }

    [Fact]
    public void Streak_resets_after_gap_without_freeze()
    {
        var user = Player(4, Day, freezes: 1);

        Streaks.Apply(user, Day.AddDays(3)); // two missed days, one freeze

        Assert.Equal((1, 1), (user.StreakCount, user.StreakFreezes));
    }

    [Fact]
    public void Reaching_7_30_and_100_days_is_a_milestone()
    {
        Assert.Equal(7, Streaks.Apply(Player(6, Day), Day.AddDays(1)));
        Assert.Null(Streaks.Apply(Player(7, Day), Day.AddDays(1)));
        Assert.Equal(100, Streaks.Apply(Player(99, Day), Day.AddDays(1)));
    }

    [Fact]
    public void A_broken_streak_shows_as_zero()
    {
        Assert.Equal(4, Streaks.Current(Player(4, Day), Day.AddDays(1)));
        Assert.Equal(4, Streaks.Current(Player(4, Day, freezes: 1), Day.AddDays(2)));
        Assert.Equal(0, Streaks.Current(Player(4, Day), Day.AddDays(2)));
    }
}
