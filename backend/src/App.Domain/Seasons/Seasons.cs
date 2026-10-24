using App.Domain.Users;

namespace App.Domain.Seasons;

// Three months of season points (idea §13). The first season starts the day it is first needed; each next one starts
// where the previous ended. Closing one (snapshot, titles, cosmetic drop) is Housekeeping's job; SP start at 0 with the next.
public class Season
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required int Number { get; init; }
    public required DateTimeOffset StartsAt { get; init; }
    public required DateTimeOffset EndsAt { get; init; }
    public DateTimeOffset? ClosedAt { get; set; }

    // The first two weeks double the SP of patched concepts, so a season starts with learning.
    public bool DoubleLearningSp(DateTimeOffset now) => now < StartsAt.AddDays(GameRules.SeasonLearningBoostDays);

    public static Season First(DateTimeOffset now)
    {
        var start = Clock.StartOf(Clock.Today(now));
        return new Season { Number = 1, StartsAt = start, EndsAt = start.AddMonths(GameRules.SeasonMonths) };
    }

    public Season Next() => new() { Number = Number + 1, StartsAt = EndsAt, EndsAt = EndsAt.AddMonths(GameRules.SeasonMonths) };
}

// The cached SP of a season (the ledger's SP rows of that season) and the best tier reached in it.
public class UserSeason
{
    public required Guid UserId { get; init; }
    public required Guid SeasonId { get; init; }
    public int Sp { get; set; }
    public required Tier BestTier { get; set; }
    public int? FinalRank { get; set; } // the season board when it closed (players with SP only)
}

public enum TitleKind { Season, Knowledge, Weekly }

// Created when first awarded; the key is the identity ('s1.top5', 'master.sql', 'weekly.top10').
public class Title
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Key { get; init; }
    public required TitleKind Kind { get; init; }
    public required string Name { get; init; }
}

public class UserTitle
{
    public required Guid UserId { get; init; }
    public required Guid TitleId { get; init; }
    public required DateTimeOffset EarnedAt { get; init; }
    public int Count { get; set; } = 1; // the weekly title counts ("3× weekly Top 10")
    public bool Active { get; set; } = true; // a knowledge title is lost while a graduated concept is below 70
}

public class UserAchievement
{
    public required Guid UserId { get; init; }
    public required string Key { get; init; }
    public required DateTimeOffset EarnedAt { get; init; }
}

// The weekly board as it stood on Monday 00:00 (idea §13).
public class WeeklyResult
{
    public required DateOnly WeekStart { get; init; }
    public required Guid UserId { get; init; }
    public required int Rank { get; init; }
    public required int Points { get; init; }
}

public static class SeasonTitles
{
    // The best of Top 5 / Top 50 / Top 100 on the final season board (idea §14).
    public static (string Key, string Name)? ForRank(int season, int rank)
    {
        foreach (var top in GameRules.SeasonTitleRanks)
            if (rank <= top) return ($"s{season}.top{top}", $"S{season} Top {top}");
        return null;
    }

    // The highest tier reached during the season ("S1 Diamond").
    public static (string Key, string Name) ForTier(int season, Tier tier) => ($"s{season}.{tier.ToString().ToLowerInvariant()}", $"S{season} {tier}");

    public static (string Key, string Name) WeeklyTop => ("weekly.top10", "Weekly Top 10");

    public static (string Key, string Name) Master(string topicSlug, string topicName) => ($"master.{topicSlug}", $"{topicName} master");
}
