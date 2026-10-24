using App.Domain.Economy;
using App.Domain.Matches;
using App.Domain.Social;
using static App.Domain.Tests.Fixtures;

namespace App.Domain.Tests;

public class SocialRulesTests
{
    [Fact]
    public void Friendship_pair_is_the_same_in_both_directions()
    {
        var (a, b) = (Guid.CreateVersion7(), Guid.CreateVersion7());

        Assert.Equal(Friendship.Pair(a, b), Friendship.Pair(b, a));
        Assert.True(string.CompareOrdinal(Friendship.Pair(a, b).Low.ToString(), Friendship.Pair(a, b).High.ToString()) < 0); // PostgreSQL's uuid order
    }

    [Fact]
    public void Friend_challenge_code_is_6_characters_from_the_alphabet()
    {
        var code = FriendChallenge.NewCode();

        Assert.Equal(6, code.Length);
        Assert.All(code, c => Assert.Contains(c, FriendChallenge.Alphabet));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(null)]
    public void Friend_match_pays_5_coins_to_both_whatever_the_result_and_no_sp(int? winner)
    {
        var match = new Match { Kind = MatchKind.Friend, RngSeed = 1, CreatedAt = Now, Phase = MatchPhase.Done, WinnerSeat = winner, ResultReason = MatchResultReason.Score };
        match.Players.Add(new MatchPlayer { Seat = 0, Kind = SeatKind.Human, UserId = Guid.NewGuid(), DisplayRating = 1000 });
        match.Players.Add(new MatchPlayer { Seat = 1, Kind = SeatKind.Human, UserId = Guid.NewGuid(), DisplayRating = 1500 });

        foreach (var seat in match.Players)
        {
            var grants = Rewards.ForMatch(match, seat, spFromWinsToday: 0);
            Assert.Equal((5, 0), (grants.Sum(g => g.Coins), grants.Sum(g => g.Sp)));
            Assert.Contains(grants, g => g.Type == RewardType.FriendMatch);
        }
    }

    [Fact]
    public void Friend_match_can_be_rematched()
    {
        var match = new Match { Kind = MatchKind.Friend, RngSeed = 1, CreatedAt = Now, Phase = MatchPhase.Done, WinnerSeat = 1, ResultReason = MatchResultReason.Score };
        match.Players.Add(new MatchPlayer { Seat = 0, Kind = SeatKind.Human, UserId = Guid.NewGuid(), DisplayRating = 1000, LastSeenAt = Now });
        match.Players.Add(new MatchPlayer { Seat = 1, Kind = SeatKind.Human, UserId = Guid.NewGuid(), DisplayRating = 1000, LastSeenAt = Now });

        Assert.True(Rematch.Available(match, 0, Now));
    }
}
