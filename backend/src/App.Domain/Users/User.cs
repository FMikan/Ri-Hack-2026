namespace App.Domain.Users;

public class User
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Email { get; set; } // stored lowercased
    public required string Username { get; set; }
    public required string UsernameNormalized { get; set; } // lower(username): unique, used for login and URLs
    public string? PasswordHash { get; set; } // null for an account that only signs in with Google
    public string? GoogleSubject { get; set; } // Google's stable account id ("sub"), set on the first Google sign-in
    public string Avatar { get; set; } = "default";
    public string Locale { get; set; } = "en";
    public ProfileVisibility ProfileVisibility { get; set; } = ProfileVisibility.Public;
    public string NotificationsJson { get; set; } = DefaultNotifications;
    public int Rating { get; set; } = GameRules.StartRating;
    public int RankedMatches { get; set; }
    public Tier Tier { get; set; } = Tier.Bronze;
    public bool IsGrandmaster { get; set; } // global top 100 (Housekeeping); shown instead of the tier
    public int Coins { get; set; }
    public int Xp { get; set; }
    public int StreakCount { get; set; }
    public DateOnly? StreakLastActiveOn { get; set; }
    public int StreakFreezes { get; set; }
    public string EquipmentJson { get; set; } = "{}";
    public Guid? EquippedTitleId { get; set; }
    public Seasons.Title? EquippedTitle { get; set; } // always loaded with the user (AutoInclude): UserRefDto shows its name
    public Guid? CurrentMatchId { get; set; } // one active match per user
    public DateTimeOffset? PremiumUntil { get; set; } // a Senior developer while this is in the future (docs/premium-pretplata.md)
    public string? PremiumInterval { get; set; } // 'monthly' | 'yearly' of the last payment
    public bool PremiumAutoRenew { get; set; }
    public string? PremiumCardLast4 { get; set; } // the only part of the card ever kept
    public DateTimeOffset? LastSeenAt { get; set; }
    public required DateTimeOffset CreatedAt { get; init; }
    public List<UserTopic> Topics { get; } = [];

    public const string DefaultNotifications = """{"dailyChallenge":true,"streakReminder":true,"rematch":true}""";

    public bool IsProvisional => RankedMatches < GameRules.ProvisionalMatches;

    public bool IsSenior(DateTimeOffset now) => PremiumUntil > now;

    public Plan PlanAt(DateTimeOffset now) => IsSenior(now) ? Plan.Senior : Plan.Junior;

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
