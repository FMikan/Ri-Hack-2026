namespace App.Domain;

// Every number from the game idea lives here (docs/tehnicki-dokument.md §4.1). Constants arrive with the phase that needs them.
public static class GameRules
{
    public const string DayTimeZone = "Europe/Zagreb";
    public const int StartRating = 1000;
    public const int ProvisionalMatches = 10; // rating shows as "?" and stays off leaderboards until then
    public const int MinTopics = 3, MaxTopics = 5;
    public const int UsernameMinLength = 3, UsernameMaxLength = 20;
    public const int PasswordMinLength = 8, PasswordMaxLength = 128;
    public const int MaxStreakFreezes = 2;
    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);
}
