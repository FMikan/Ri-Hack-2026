using App.Domain.Content;
using App.Domain.Economy;
using App.Domain.Seasons;
using App.Domain.Users;
using static App.Domain.Tests.Fixtures;

namespace App.Domain.Tests;

public class SeasonRulesTests
{
    [Fact]
    public void First_season_starts_today_and_doubles_learning_sp_for_two_weeks()
    {
        var season = Season.First(Now);

        Assert.Equal((1, Clock.StartOf(Clock.Today(Now))), (season.Number, season.StartsAt));
        Assert.Equal(season.StartsAt.AddMonths(GameRules.SeasonMonths), season.EndsAt);
        Assert.True(season.DoubleLearningSp(season.StartsAt.AddDays(13)));
        Assert.False(season.DoubleLearningSp(season.StartsAt.AddDays(14)));
    }

    [Fact]
    public void Next_season_starts_where_the_last_one_ended()
    {
        var next = Season.First(Now).Next();

        Assert.Equal((2, Season.First(Now).EndsAt), (next.Number, next.StartsAt));
        Assert.Equal(next.StartsAt.AddMonths(GameRules.SeasonMonths), next.EndsAt);
    }

    [Fact]
    public void Patched_sp_doubles_with_the_season_boost_and_so_does_the_daily_cap()
    {
        var concepts = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToList();

        var normal = Rewards.ForLesson(Guid.NewGuid(), Guid.NewGuid(), concepts[..2], spFromLearningToday: 0);
        var boosted = Rewards.ForLesson(Guid.NewGuid(), Guid.NewGuid(), concepts[..2], spFromLearningToday: 0, spMultiplier: 2);
        var capped = Rewards.ForLesson(Guid.NewGuid(), Guid.NewGuid(), concepts, spFromLearningToday: 0, spMultiplier: 2);

        Assert.Equal(4, normal.Sum(g => g.Sp));
        Assert.Equal(8, boosted.Sum(g => g.Sp));
        Assert.Equal(20, capped.Sum(g => g.Sp));
    }

    [Theory]
    [InlineData(1, "s3.top5")]
    [InlineData(5, "s3.top5")]
    [InlineData(6, "s3.top50")]
    [InlineData(100, "s3.top100")]
    [InlineData(101, null)]
    public void Season_rank_titles_go_to_top_5_50_100(int rank, string? key) =>
        Assert.Equal(key, SeasonTitles.ForRank(3, rank)?.Key);

    [Fact]
    public void Season_tier_title_names_the_best_tier() =>
        Assert.Equal(("s1.diamond", "S1 Diamond"), SeasonTitles.ForTier(1, Tier.Diamond));

    [Fact]
    public void First_win_achievement_fires_once()
    {
        var snapshot = new UserSnapshot(Wins: 1, Brilliants: 0, BestStreak: 0, ConceptsPatched: 0, UpsetWins: 0, TopicsAbove50: 0, Topics: 3, DailyChallengeRun: 0);

        Assert.Contains("first_win", AchievementRules.Evaluate(snapshot, new HashSet<string>()));
        Assert.DoesNotContain("first_win", AchievementRules.Evaluate(snapshot, new HashSet<string> { "first_win" }));
        Assert.DoesNotContain("brilliant_10", AchievementRules.Evaluate(snapshot, new HashSet<string>()));
    }

    [Fact]
    public void All_topics_achievement_needs_every_active_topic_above_50()
    {
        var some = new UserSnapshot(0, 0, 0, 0, 0, TopicsAbove50: 2, Topics: 3, 0);
        var none = new UserSnapshot(0, 0, 0, 0, 0, TopicsAbove50: 0, Topics: 0, 0);

        Assert.DoesNotContain("all_topics_50", AchievementRules.Evaluate(some, new HashSet<string>()));
        Assert.Contains("all_topics_50", AchievementRules.Evaluate(some with { TopicsAbove50 = 3 }, new HashSet<string>()));
        Assert.DoesNotContain("all_topics_50", AchievementRules.Evaluate(none, new HashSet<string>()));
    }

    [Theory]
    [InlineData(2, 50, 0.90, 1)] // too easy: a band down
    [InlineData(2, 50, 0.20, 3)] // too hard: a band up
    [InlineData(1, 80, 0.95, 1)]
    [InlineData(4, 80, 0.05, 4)]
    [InlineData(2, 49, 0.90, 2)] // not enough answers yet
    [InlineData(2, 60, 0.50, 2)]
    public void Question_difficulty_moves_a_band_after_enough_answers(int difficulty, int answers, double correctRatio, int expected) =>
        Assert.Equal(expected, QuestionCalibration.Next(difficulty, answers, correctRatio));
}
