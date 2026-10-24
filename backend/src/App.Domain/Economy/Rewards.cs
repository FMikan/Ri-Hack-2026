using App.Domain.Matches;
using App.Domain.Users;

namespace App.Domain.Economy;

public enum RewardType
{
    MatchWin, MatchLoss, MatchDraw, RatingUpset, LessonPassed, ConceptPatched, DailyChallenge, DailyTask, DailyTaskBonus,
    StreakMilestone, WeeklyXpGoal, LevelUp, FriendMatch, AnswerXp, MatchXp, ShopPurchase, StreakFreeze, SeasonReset, SeasonDrop,
}

// The ledger (docs §3.7): the only way coins, XP and SP change. users.coins and users.xp are its cached sums.
public class RewardEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserId { get; init; }
    public required RewardType Type { get; init; }
    public int Coins { get; init; } // negative for a purchase
    public int Xp { get; init; }
    public int Sp { get; init; }
    public Guid? ReferenceId { get; init; }
    public required string DedupeKey { get; init; } // unique: the same reward is never paid twice
    public required DateTimeOffset CreatedAt { get; init; }
}

public record Grant(RewardType Type, int Coins, int Xp, int Sp, Guid? ReferenceId, string DedupeKey);

public static class Rewards
{
    // The reward matrix (docs §4.3) for one human seat of a finished match. Calibration and aborted matches pay nothing.
    public static List<Grant> ForMatch(Match match, MatchPlayer seat, int spFromWinsToday)
    {
        if (match.Kind == MatchKind.Calibration || match.ResultReason == MatchResultReason.Aborted) return [];
        var user = seat.UserId!.Value;
        var answered = match.Answers.Count(a => a.Seat == seat.Seat && !a.TimedOut);
        List<Grant> grants =
        [
            new(RewardType.MatchXp, 0, GameRules.XpMatch, 0, match.Id, Key(RewardType.MatchXp, match.Id, user)),
            new(RewardType.AnswerXp, 0, GameRules.XpAnswer * answered, 0, match.Id, Key(RewardType.AnswerXp, match.Id, user)),
        ];
        // A friend duel is unranked: 5 coins for both whatever the result, no SP (idea §12).
        if (match.Kind == MatchKind.Friend)
            grants.Add(new Grant(RewardType.FriendMatch, GameRules.CoinsFriendMatch, 0, 0, match.Id, Key(RewardType.FriendMatch, match.Id, user)));
        if (match.Kind is not (MatchKind.Ranked or MatchKind.Ghost)) return grants;

        var opponent = match.Players.First(p => p.Seat != seat.Seat);
        var (type, coins) = match.WinnerSeat is null ? (RewardType.MatchDraw, GameRules.CoinsDraw)
            : match.WinnerSeat == seat.Seat ? (RewardType.MatchWin, GameRules.CoinsWin)
            : (RewardType.MatchLoss, GameRules.CoinsLoss);
        // SP only for a win, at most 100 a day from wins (idea §9), the upset bonus included.
        var spLeft = Math.Max(0, GameRules.SpDailyWinCap - spFromWinsToday);
        var sp = type == RewardType.MatchWin ? Math.Min(GameRules.SpWin, spLeft) : 0;
        grants.Add(new Grant(type, coins, 0, sp, match.Id, Key(type, match.Id, user)));
        if (type == RewardType.MatchWin && opponent.DisplayRating - seat.DisplayRating >= GameRules.SpUpsetGap && spLeft - sp > 0)
            grants.Add(new Grant(RewardType.RatingUpset, 0, 0, Math.Min(GameRules.SpUpset, spLeft - sp), match.Id, Key(RewardType.RatingUpset, match.Id, user)));
        return grants;
    }

    // A passed, reward-eligible lesson: 50 coins and 100 XP when at least one concept was not rewarded in the last 7 days,
    // and 2 SP for each such concept, at most 10 SP a day from learning.
    public static List<Grant> ForLesson(Guid user, Guid lessonId, IReadOnlyList<Guid> eligibleConceptIds, int spFromLearningToday)
    {
        if (eligibleConceptIds.Count == 0) return [];
        List<Grant> grants = [new(RewardType.LessonPassed, GameRules.CoinsLessonPassed, GameRules.XpLesson, 0, lessonId, $"lesson_passed:{lessonId}")];
        var spLeft = Math.Max(0, GameRules.SpDailyLearnCap - spFromLearningToday);
        foreach (var concept in eligibleConceptIds)
        {
            var sp = Math.Min(GameRules.SpPatched, spLeft);
            spLeft -= sp;
            grants.Add(new Grant(RewardType.ConceptPatched, 0, 0, sp, concept, $"concept_patched:{concept}:{user}:{lessonId}"));
        }
        return grants;
    }

    // 50 × n coins for every level n crossed by an XP gain.
    public static List<Grant> LevelUps(Guid user, int oldXp, int newXp) =>
        Enumerable.Range(Level.Of(oldXp) + 1, Math.Max(0, Level.Of(newXp) - Level.Of(oldXp)))
            .Select(n => new Grant(RewardType.LevelUp, GameRules.CoinsPerLevel * n, 0, 0, null, $"level_up:{user}:{n}"))
            .ToList();

    public static string Key(RewardType type, Guid reference, Guid user) => $"{Name(type)}:{reference}:{user}";

    public static string Name(RewardType type) => System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(type.ToString());
}
