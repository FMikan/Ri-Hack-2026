using App.Domain.Users;

namespace App.Domain.Matches;

// R' = R + K(S − E), E = 1 / (1 + 10^((Rb − Ra) / 400)); K = 32 for the first 30 ranked matches, then 16 (idea §9).
public static class Elo
{
    public static int Update(int rating, int opponentRating, double score, int rankedMatches)
    {
        var expected = 1 / (1 + Math.Pow(10, (opponentRating - rating) / 400.0));
        var k = rankedMatches < GameRules.HighKMatches ? GameRules.KHigh : GameRules.KLow;
        return (int)Math.Round(rating + k * (score - expected), MidpointRounding.AwayFromZero);
    }
}

public static class RankedResult
{
    // A finished ranked match moves both ratings (draw = 0.5), counts the match and re-derives the tiers.
    // An aborted match (both players gone) changes nothing (idea §17).
    public static void Apply(Match match)
    {
        if (match.Kind != MatchKind.Ranked || match.ResultReason == MatchResultReason.Aborted) return;
        var (a, b) = (match.Players[0], match.Players[1]);
        var (userA, userB) = (a.User!, b.User!);
        var scoreA = match.WinnerSeat is null ? 0.5 : match.WinnerSeat == a.Seat ? 1 : 0;
        var newA = Elo.Update(userA.Rating, userB.Rating, scoreA, userA.RankedMatches);
        var newB = Elo.Update(userB.Rating, userA.Rating, 1 - scoreA, userB.RankedMatches);
        foreach (var (player, user, rating) in new[] { (a, userA, newA), (b, userB, newB) })
        {
            (player.RatingBefore, player.RatingAfter) = (user.Rating, rating);
            user.Rating = rating;
            user.RankedMatches++;
            user.Tier = Tiers.Of(rating, user.Tier);
        }
    }
}
