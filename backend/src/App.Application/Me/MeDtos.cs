using System.ComponentModel.DataAnnotations;
using App.Application.Content;
using App.Domain;
using App.Domain.Content;
using App.Domain.Progress;
using App.Domain.Users;

namespace App.Application.Me;

// Mirrors docs/tehnicki-dokument.md §6.3. Keep frontend/src/types/api.ts in sync.
public record UserRefDto(Guid Id, string Username, string Avatar, string Tier, int Rating, bool IsProvisional, string? Title, Dictionary<string, string> Equipment)
{
    // ponytail: no titles or cosmetics before F4/F5; then fill Title and map equipped item ids to asset keys here.
    public static UserRefDto From(User user) =>
        new(user.Id, user.Username, user.Avatar, user.Tier.ToString().ToLowerInvariant(), user.Rating, user.IsProvisional, null, []);
}

public record StreakDto(int Count, DateOnly? LastActiveOn, int Freezes, bool SecuredToday);

public record MeDto(
    Guid Id, string Username, string Avatar, string Tier, int Rating, bool IsProvisional, string? Title, Dictionary<string, string> Equipment,
    string Email, string Locale, ProfileVisibility ProfileVisibility, Dictionary<string, bool> Notifications,
    int RankedMatches, int Coins, int Xp, int Level, int XpToNextLevel, int SeasonPoints, StreakDto Streak,
    Guid[] ActiveTopicIds, Guid? EquippedTitleId, Guid? ActiveMatchId, bool InQueue, DateTimeOffset CreatedAt);

public record UpdateMeRequest
{
    [StringLength(GameRules.UsernameMaxLength, MinimumLength = GameRules.UsernameMinLength)]
    [RegularExpression("^[A-Za-z0-9_]+$", ErrorMessage = "Username may contain letters, digits and underscores only.")]
    public string? Username { get; init; }

    [StringLength(40)]
    public string? Avatar { get; init; }

    [RegularExpression("^(en|hr)$", ErrorMessage = "Locale must be 'en' or 'hr'.")]
    public string? Locale { get; init; }

    public ProfileVisibility? ProfileVisibility { get; init; }
    public Dictionary<string, bool>? Notifications { get; init; }
}

public record SetTopicsRequest
{
    [Required]
    public required Guid[] TopicIds { get; init; }
}

public record DashboardTopicDto(TopicDto Topic, bool Active, double? Mastery, double Completion);
public record RatingPointDto(DateTimeOffset At, int Rating);
public record SuggestionDto(string Kind, Guid? TopicId, Guid[]? ConceptIds);
public record DailyTaskDto(string Key, int Progress, int Target, bool Completed, int Reward, TopicDto? Topic);
public record RematchInboxDto(Guid MatchId, UserRefDto From, DateTimeOffset ExpiresAt);
public record FriendChallengeInboxDto(string Code, UserRefDto Host, DateTimeOffset ExpiresAt);
public record InboxDto(RematchInboxDto? Rematch, FriendChallengeInboxDto? FriendChallenge);

public record DashboardDto(
    MeDto Me, DashboardTopicDto[] Topics, RatingPointDto[] RatingHistory, int DueReviews, DailyTaskDto[] DailyTasks,
    bool DailyChallengeAttempted, SuggestionDto? Suggestion, InboxDto Inbox, bool QueueLocked);

public record DueConceptDto(Guid Id, string Slug, string Name, Guid TopicId, DateTimeOffset ReviewDueAt, int ReviewStep, TopicDto Topic);

public record DueConceptsDto(DueConceptDto[] Concepts);

public record LastAnswerDto(Guid QuestionId, QuestionType Type, string Prompt, bool IsCorrect, DateTimeOffset At);

public record KnowledgeConceptDto(ConceptDto Concept, double Mastery, ConceptState State, DateTimeOffset? ReviewDueAt, int? ReviewStep, LastAnswerDto[] LastAnswers);

// mastery: average over answered concepts (null until one is answered); completion: % of the topic's concepts graduated.
public record KnowledgeTopicDto(TopicDto Topic, bool Active, double? Mastery, double Completion, bool CalibrationAvailable, KnowledgeConceptDto[] Concepts);

public record KnowledgeMapDto(KnowledgeTopicDto[] Topics);

