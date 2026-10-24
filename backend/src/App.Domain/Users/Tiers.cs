namespace App.Domain.Users;

public static class Tiers
{
    // Up as soon as the rating reaches a threshold; down only 25 below the current tier's threshold (idea §14).
    public static Tier Of(int rating, Tier current)
    {
        var raw = (Tier)GameRules.TierThresholds.Count(t => rating >= t);
        if (raw >= current || current == Tier.Bronze) return raw;
        return rating >= GameRules.TierThresholds[(int)current - 1] - GameRules.TierHysteresis ? current : raw;
    }
}
