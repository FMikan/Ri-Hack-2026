using App.Domain.Matches;
using App.Domain.Users;
using static App.Domain.Tests.Fixtures;

namespace App.Domain.Tests;

// The Senior developer subscription (docs/premium-pretplata.md): what the multipliers do to a rating change.
public class PremiumTests
{
    private static User Player(string name, int rating, DateTimeOffset? premiumUntil = null) =>
        new() { Email = name, Username = name, UsernameNormalized = name, PasswordHash = "x", CreatedAt = Now, Rating = rating, RankedMatches = 40, PremiumUntil = premiumUntil };

    private static Match Ranked(User a, User b, int winnerSeat)
    {
        var match = new Match { Kind = MatchKind.Ranked, RngSeed = 1, CreatedAt = Now, Phase = MatchPhase.Done, WinnerSeat = winnerSeat, ResultReason = MatchResultReason.Score, FinishedAt = Now };
        match.Players.Add(new MatchPlayer { Seat = 0, Kind = SeatKind.Human, UserId = Guid.NewGuid(), User = a, DisplayRating = a.Rating });
        match.Players.Add(new MatchPlayer { Seat = 1, Kind = SeatKind.Human, UserId = Guid.NewGuid(), User = b, DisplayRating = b.Rating });
        return match;
    }

    [Fact]
    public void Senior_rating_gain_is_20_percent_larger_and_the_loss_is_not()
    {
        var senior = Player("senior", 1000, premiumUntil: Now.AddDays(10));
        var junior = Player("junior", 1000);

        RankedResult.Apply(Ranked(senior, junior, winnerSeat: 0));

        Assert.Equal((1010, 992), (senior.Rating, junior.Rating)); // K = 16 at equal ratings: +8 becomes +10, -8 stays -8

        RankedResult.Apply(Ranked(senior, junior, winnerSeat: 1));

        Assert.Equal(1010 - 8, senior.Rating); // a loss costs the same as for anyone (K/2 at these ratings, rounded)
    }

    [Fact]
    public void An_expired_subscription_boosts_nothing()
    {
        var expired = Player("expired", 1000, premiumUntil: Now.AddDays(-1));
        var other = Player("other", 1000);

        RankedResult.Apply(Ranked(expired, other, winnerSeat: 0));

        Assert.Equal(1008, expired.Rating);
    }

    [Theory]
    [InlineData(100, 120)]
    [InlineData(5, 6)]
    [InlineData(0, 0)]
    [InlineData(-8, -8)]
    public void Boost_rounds_half_up_and_leaves_losses_alone(int amount, int boosted) =>
        Assert.Equal(boosted, Premium.Boost(amount, Premium.XpMultiplier));

    [Fact]
    public void Plan_follows_premium_until()
    {
        var user = Player("ana", 1000, premiumUntil: Now.AddDays(1));

        Assert.Equal(Plan.Senior, user.PlanAt(Now));
        Assert.Equal(Plan.Junior, user.PlanAt(Now.AddDays(2)));
        Assert.Equal(Plan.Junior, Player("ben", 1000).PlanAt(Now));
    }
}
