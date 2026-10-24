using App.Application;
using App.Domain.Content;
using App.Domain.Matches;
using App.Domain.Progress;
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
    public DbSet<ConceptMastery> ConceptMastery => Set<ConceptMastery>();
    public DbSet<Answer> Answers => Set<Answer>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchPlayer> MatchPlayers => Set<MatchPlayer>();
    public DbSet<MatchQuestion> MatchQuestions => Set<MatchQuestion>();
    public DbSet<MatchAnswer> MatchAnswers => Set<MatchAnswer>();

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
    }
}
