using App.Application;
using App.Domain.Content;
using App.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserTopic> UserTopics => Set<UserTopic>();
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<Concept> Concepts => Set<Concept>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

    // Enums are stored as snake_case text so the database, the CHECK constraints and the API share one spelling.
    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<Tier>().HaveConversion<SnakeCaseEnumConverter<Tier>>();
        builder.Properties<ProfileVisibility>().HaveConversion<SnakeCaseEnumConverter<ProfileVisibility>>();
    }
}
