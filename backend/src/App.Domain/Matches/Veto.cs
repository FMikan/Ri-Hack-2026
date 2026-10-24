namespace App.Domain.Matches;

public record VetoBan(Guid TopicId, int Seat, bool Auto);

// The veto of a ranked match (idea §5), stored as matches.veto (jsonb).
public class VetoState
{
    public List<Guid> Pool { get; set; } = [];
    public List<List<Guid>> Picks { get; set; } = []; // each seat's active topics, for the "picked by the opponent" icon
    public int CoinWinnerSeat { get; set; }
    public int? TurnSeat { get; set; } // null once two topics remain
    public List<VetoBan> Bans { get; set; } = [];
    public List<Guid>? FinalTwo { get; set; }
    public Guid? ChosenTopicId { get; set; }

    public List<Guid> Remaining => Pool.Where(t => Bans.All(b => b.TopicId != t)).ToList();
    public int BansRemaining => Math.Max(0, Remaining.Count - 2);
}

public static class Veto
{
    // Shared topics always; the rest of the union fills up to six at random (3–6 topics).
    public static List<Guid> BuildPool(IReadOnlyCollection<Guid> topicsA, IReadOnlyCollection<Guid> topicsB, Random rng)
    {
        var shared = topicsA.Intersect(topicsB).ToList();
        var rest = topicsA.Union(topicsB).Except(shared).ToArray();
        rng.Shuffle(rest);
        var pool = shared.Concat(rest).Take(Math.Max(GameRules.VetoPoolMax, shared.Count)).ToArray();
        rng.Shuffle(pool);
        return [.. pool];
    }

    public static int BansFor(int poolSize) => poolSize - 2;

    // The coin flip decides who bans first.
    public static VetoState Start(IReadOnlyCollection<Guid> topicsA, IReadOnlyCollection<Guid> topicsB, Random rng)
    {
        var coin = rng.Next(2);
        var veto = new VetoState { Pool = BuildPool(topicsA, topicsB, rng), Picks = [[.. topicsA], [.. topicsB]], CoinWinnerSeat = coin, TurnSeat = coin };
        if (veto.Pool.Count <= 2) (veto.TurnSeat, veto.FinalTwo) = (null, veto.Remaining); // nothing to ban
        return veto;
    }

    public static void Ban(VetoState veto, int seat, Guid topicId, bool auto = false)
    {
        if (veto.TurnSeat != seat)
            throw new AppException(ErrorCodes.MatchNotYourTurn, "It is the other player's turn to ban.");
        if (!veto.Remaining.Contains(topicId))
            throw new AppException(ErrorCodes.MatchTopicNotInPool, "This topic is not in the pool any more.");
        veto.Bans.Add(new VetoBan(topicId, seat, auto));
        if (veto.Remaining.Count > 2)
        {
            veto.TurnSeat = 1 - seat;
            return;
        }
        veto.TurnSeat = null;
        veto.FinalTwo = veto.Remaining;
    }

    // The 8 s ran out: a random topic is banned for the player on turn.
    public static void AutoBan(VetoState veto, Random rng)
    {
        var remaining = veto.Remaining;
        Ban(veto, veto.TurnSeat!.Value, remaining[rng.Next(remaining.Count)], auto: true);
    }

    // Nobody chooses the topic: a random one of the last two (removes the last ban's advantage).
    public static Guid Pick(VetoState veto, Random rng)
    {
        veto.ChosenTopicId = veto.FinalTwo![rng.Next(veto.FinalTwo.Count)];
        return veto.ChosenTopicId.Value;
    }
}
