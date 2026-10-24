namespace App.Domain.Users;

public class User
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Email { get; set; } // stored lowercased
    public required string Username { get; set; }
    public required string UsernameNormalized { get; set; } // lower(username): unique, used for login and URLs
    public required string PasswordHash { get; set; }
    public string Avatar { get; set; } = "default";
    public string Locale { get; set; } = "en";
    public ProfileVisibility ProfileVisibility { get; set; } = ProfileVisibility.Public;
    public string NotificationsJson { get; set; } = DefaultNotifications;
    public int Rating { get; set; } = GameRules.StartRating;
    public int RankedMatches { get; set; }
    public Tier Tier { get; set; } = Tier.Bronze;
    public int Coins { get; set; }
    public int Xp { get; set; }
    public int StreakCount { get; set; }
    public DateOnly? StreakLastActiveOn { get; set; }
    public int StreakFreezes { get; set; }
    public string EquipmentJson { get; set; } = "{}";
    public Guid? CurrentMatchId { get; set; } // one active match per user
    public DateTimeOffset? LastSeenAt { get; set; }
    public required DateTimeOffset CreatedAt { get; init; }
    public List<UserTopic> Topics { get; } = [];

    public const string DefaultNotifications = """{"dailyChallenge":true,"streakReminder":true,"rematch":true}""";

    public bool IsProvisional => RankedMatches < GameRules.ProvisionalMatches;

    public static string Normalize(string username) => username.ToLowerInvariant();
}

public enum ProfileVisibility { Public, Friends }

// Rank tier derived from Elo with hysteresis (docs §4.2); Grandmaster is a separate flag computed from the leaderboard.
public enum Tier { Bronze, Silver, Gold, Platinum, Diamond, Master }

public class UserTopic
{
    public Guid UserId { get; init; }
    public Guid TopicId { get; init; }
}
