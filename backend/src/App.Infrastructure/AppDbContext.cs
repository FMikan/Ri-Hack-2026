using App.Application;
using App.Domain.Content;
using App.Domain.Daily;
using App.Domain.Economy;
using App.Domain.Lessons;
using App.Domain.Matches;
using App.Domain.Matchmaking;
using App.Domain.Progress;
using App.Domain.Seasons;
using App.Domain.Social;
using App.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<UserTopic> UserTopics => Set<UserTopic>();
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<Concept> Concepts => Set<Concept>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<QuestionReport> QuestionReports => Set<QuestionReport>();
    public DbSet<ConceptMastery> ConceptMastery => Set<ConceptMastery>();
    public DbSet<Answer> Answers => Set<Answer>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchPlayer> MatchPlayers => Set<MatchPlayer>();
    public DbSet<MatchQuestion> MatchQuestions => Set<MatchQuestion>();
    public DbSet<MatchAnswer> MatchAnswers => Set<MatchAnswer>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<QueueEntry> QueueEntries => Set<QueueEntry>();
    public DbSet<MatchProposal> MatchProposals => Set<MatchProposal>();
    public DbSet<GhostOffer> GhostOffers => Set<GhostOffer>();
    public DbSet<RewardEvent> RewardEvents => Set<RewardEvent>();
    public DbSet<ActivityDay> ActivityDays => Set<ActivityDay>();
    public DbSet<UserDailyTask> UserDailyTasks => Set<UserDailyTask>();
    public DbSet<DailyChallenge> DailyChallenges => Set<DailyChallenge>();
    public DbSet<DailyChallengeAttempt> DailyChallengeAttempts => Set<DailyChallengeAttempt>();
    public DbSet<ShopItem> ShopItems => Set<ShopItem>();
    public DbSet<UserItem> UserItems => Set<UserItem>();
    public DbSet<ConceptLessonCards> ConceptLessonCards => Set<ConceptLessonCards>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<FriendChallenge> FriendChallenges => Set<FriendChallenge>();
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<UserSeason> UserSeasons => Set<UserSeason>();
    public DbSet<Title> Titles => Set<Title>();
    public DbSet<UserTitle> UserTitles => Set<UserTitle>();
    public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();
    public DbSet<WeeklyResult> WeeklyResults => Set<WeeklyResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

    // Enums are stored as snake_case text so the database, the CHECK constraints and the API share one spelling.
    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<Tier>().HaveConversion<SnakeCaseEnumConverter<Tier>>();
        builder.Properties<ProfileVisibility>().HaveConversion<SnakeCaseEnumConverter<ProfileVisibility>>();
        builder.Properties<QuestionType>().HaveConversion<SnakeCaseEnumConverter<QuestionType>>();
        builder.Properties<QuestionStatus>().HaveConversion<SnakeCaseEnumConverter<QuestionStatus>>();
        builder.Properties<AnswerLabel>().HaveConversion<SnakeCaseEnumConverter<AnswerLabel>>();
        builder.Properties<AnswerSource>().HaveConversion<SnakeCaseEnumConverter<AnswerSource>>();
        builder.Properties<MatchKind>().HaveConversion<SnakeCaseEnumConverter<MatchKind>>();
        builder.Properties<MatchPhase>().HaveConversion<SnakeCaseEnumConverter<MatchPhase>>();
        builder.Properties<MatchResultReason>().HaveConversion<SnakeCaseEnumConverter<MatchResultReason>>();
        builder.Properties<MatchSlot>().HaveConversion<SnakeCaseEnumConverter<MatchSlot>>();
        builder.Properties<SeatKind>().HaveConversion<SnakeCaseEnumConverter<SeatKind>>();
        builder.Properties<BotPersona>().HaveConversion<SnakeCaseEnumConverter<BotPersona>>();
        builder.Properties<LessonStatus>().HaveConversion<SnakeCaseEnumConverter<LessonStatus>>();
        builder.Properties<QueueStatus>().HaveConversion<SnakeCaseEnumConverter<QueueStatus>>();
        builder.Properties<ProposalStatus>().HaveConversion<SnakeCaseEnumConverter<ProposalStatus>>();
        builder.Properties<RematchStatus>().HaveConversion<SnakeCaseEnumConverter<RematchStatus>>();
        builder.Properties<RewardType>().HaveConversion<SnakeCaseEnumConverter<RewardType>>();
        builder.Properties<ShopItemKind>().HaveConversion<SnakeCaseEnumConverter<ShopItemKind>>();
        builder.Properties<LessonCardsSource>().HaveConversion<SnakeCaseEnumConverter<LessonCardsSource>>();
        builder.Properties<FriendshipStatus>().HaveConversion<SnakeCaseEnumConverter<FriendshipStatus>>();
        builder.Properties<TitleKind>().HaveConversion<SnakeCaseEnumConverter<TitleKind>>();
    }
}
