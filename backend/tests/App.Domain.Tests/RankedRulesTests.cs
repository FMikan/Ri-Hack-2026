using App.Domain.Matches;
using App.Domain.Matchmaking;
using App.Domain.Users;
using static App.Domain.Tests.Fixtures;

namespace App.Domain.Tests;

public class EloTests
{
    [Fact]
    public void Equal_ratings_win_gives_plus_16_with_K32() => Assert.Equal(1016, Elo.Update(1000, 1000, 1, rankedMatches: 0));

    [Fact]
    public void K_drops_to_16_after_30_ranked_matches()
    {
        Assert.Equal(1016, Elo.Update(1000, 1000, 1, rankedMatches: 29));
        Assert.Equal(1008, Elo.Update(1000, 1000, 1, rankedMatches: 30));
    }

    [Fact]
    public void Draw_between_equal_ratings_changes_nothing() => Assert.Equal(1000, Elo.Update(1000, 1000, 0.5, rankedMatches: 3));

    [Fact]
    public void Upset_win_against_200_higher_gives_24() => Assert.Equal(1024, Elo.Update(1000, 1200, 1, rankedMatches: 3));

    [Fact]
    public void Loss_is_the_mirror_of_the_win() => Assert.Equal(984, Elo.Update(1000, 1000, 0, rankedMatches: 3));
}

public class TierTests
{
    [Theory]
    [InlineData(1099, Tier.Bronze)]
    [InlineData(1100, Tier.Silver)]
    [InlineData(1300, Tier.Gold)]
    [InlineData(1500, Tier.Platinum)]
    [InlineData(1700, Tier.Diamond)]
    [InlineData(1900, Tier.Master)]
    public void Rating_maps_to_tier_on_the_way_up(int rating, Tier tier) => Assert.Equal(tier, Tiers.Of(rating, Tier.Bronze));

    [Fact]
    public void Gold_drops_to_silver_only_below_1275()
    {
        Assert.Equal(Tier.Gold, Tiers.Of(1275, Tier.Gold));
        Assert.Equal(Tier.Silver, Tiers.Of(1274, Tier.Gold));
    }

    [Fact]
    public void Rating_1299_from_silver_stays_silver() => Assert.Equal(Tier.Silver, Tiers.Of(1299, Tier.Silver));

    [Fact]
    public void A_big_drop_skips_the_tiers_in_between() => Assert.Equal(Tier.Bronze, Tiers.Of(1050, Tier.Platinum));
}

public class VetoTests
{
    private static readonly Guid[] T = Enumerable.Range(1, 8).Select(i => new Guid(i, 0, 0, new byte[8])).ToArray();

    [Fact]
    public void Pool_always_includes_shared_topics()
    {
        foreach (var seed in Enumerable.Range(0, 20))
        {
            var pool = Veto.BuildPool([T[0], T[1], T[2], T[3], T[4]], [T[0], T[5], T[6], T[7], T[1]], new Random(seed));
            Assert.Contains(T[0], pool);
            Assert.Contains(T[1], pool);
        }
    }

    [Fact]
    public void Pool_is_capped_at_6_and_has_no_duplicates()
    {
        var pool = Veto.BuildPool([T[0], T[1], T[2], T[3], T[4]], [T[5], T[6], T[7]], new Random(1));

        Assert.Equal(6, pool.Count);
        Assert.Equal(6, pool.Distinct().Count());
    }

    [Theory]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(5, 3)]
    [InlineData(6, 4)]
    public void Bans_for_pool_size_3_4_5_6_are_1_2_3_4(int pool, int bans) => Assert.Equal(bans, Veto.BansFor(pool));

    private static VetoState FourTopics(int coinWinner = 0) =>
        new() { Pool = [T[0], T[1], T[2], T[3]], Picks = [[T[0], T[1], T[2]], [T[0], T[3], T[1]]], CoinWinnerSeat = coinWinner, TurnSeat = coinWinner };

    [Fact]
    public void Bans_alternate_until_two_topics_remain()
    {
        var veto = FourTopics(coinWinner: 1);

        Veto.Ban(veto, 1, T[2]);
        Assert.Equal(0, veto.TurnSeat);
        Veto.Ban(veto, 0, T[3]);

        Assert.Null(veto.TurnSeat);
        Assert.Equal([T[0], T[1]], veto.FinalTwo);
        Assert.Equal(0, veto.BansRemaining);
    }

    [Fact]
    public void Ban_out_of_turn_is_rejected() =>
        Assert.Equal(ErrorCodes.MatchNotYourTurn, Assert.Throws<AppException>(() => Veto.Ban(FourTopics(), 1, T[2])).Code);

    [Fact]
    public void Ban_of_banned_or_unknown_topic_is_rejected()
    {
        var veto = FourTopics();
        Veto.Ban(veto, 0, T[2]);

        Assert.Equal(ErrorCodes.MatchTopicNotInPool, Assert.Throws<AppException>(() => Veto.Ban(veto, 1, T[2])).Code);
        Assert.Equal(ErrorCodes.MatchTopicNotInPool, Assert.Throws<AppException>(() => Veto.Ban(veto, 1, T[7])).Code);
    }

    [Fact]
    public void Auto_ban_picks_random_unbanned_with_seed()
    {
        var a = FourTopics();
        var b = FourTopics();

        Veto.AutoBan(a, new Random(7));
        Veto.AutoBan(b, new Random(7));

        var ban = Assert.Single(a.Bans);
        Assert.True(ban.Auto);
        Assert.Equal(0, ban.Seat);
        Assert.Equal(ban, Assert.Single(b.Bans));
    }

    [Fact]
    public void Two_remaining_topics_pick_randomly_with_seed()
    {
        var picks = Enumerable.Range(0, 40).Select(seed =>
        {
            var veto = FourTopics();
            Veto.Ban(veto, 0, T[2]);
            Veto.Ban(veto, 1, T[3]);
            return Veto.Pick(veto, new Random(seed));
        }).ToList();

        Assert.All(picks, p => Assert.Contains(p, new[] { T[0], T[1] }));
        Assert.Equal(2, picks.Distinct().Count());
    }
}

public class MatchmakingRulesTests
{
    private static readonly Guid Sql = Guid.NewGuid(), Js = Guid.NewGuid(), Ai = Guid.NewGuid(), Net = Guid.NewGuid();

    private static QueueCandidate Player(int rating, Guid[] topics, int waitedSeconds = 0) =>
        new(Guid.NewGuid(), rating, topics, Now.AddSeconds(-waitedSeconds));

    [Theory]
    [InlineData(0, 100)]
    [InlineData(9, 100)]
    [InlineData(10, 150)]
    [InlineData(25, 200)]
    [InlineData(180, 400)]
    public void Range_widens_by_50_every_10_seconds_up_to_400(int seconds, int range) =>
        Assert.Equal(range, MatchmakingRules.Range(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void Smallest_gap_wins_then_more_shared_topics_then_longer_wait()
    {
        var me = Player(1000, [Sql, Js, Ai]);
        var far = Player(1080, [Sql, Js, Ai]);
        var closeOneTopic = Player(1020, [Sql], waitedSeconds: 50);
        var closeTwoTopics = Player(980, [Sql, Js]);
        var closeTwoTopicsOlder = Player(1020, [Js, Ai], waitedSeconds: 5);

        Assert.Equal(closeTwoTopicsOlder, MatchmakingRules.Pick(me, [far, closeOneTopic, closeTwoTopics, closeTwoTopicsOlder], [], Now));
    }

    [Fact]
    public void No_shared_topic_or_a_gap_beyond_the_range_never_matches()
    {
        var me = Player(1000, [Sql, Js, Ai]);

        Assert.Null(MatchmakingRules.Pick(me, [Player(1000, [Net])], [], Now));
        Assert.Null(MatchmakingRules.Pick(me, [Player(1150, [Sql])], [], Now));
        Assert.NotNull(MatchmakingRules.Pick(me with { EnteredAt = Now.AddSeconds(-10) }, [Player(1150, [Sql])], [], Now));
    }

    [Fact]
    public void A_third_ranked_match_in_a_row_against_the_same_opponent_is_not_proposed()
    {
        var me = Player(1000, [Sql]);
        var same = Player(1000, [Sql]);
        var other = Player(1090, [Sql]);

        Assert.Equal(other, MatchmakingRules.Pick(me, [same, other], [same.UserId, same.UserId], Now));
        Assert.Equal(same, MatchmakingRules.Pick(me, [same, other], [same.UserId, other.UserId], Now));
    }
}
