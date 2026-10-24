using App.Domain;
using App.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasIndex(u => u.UsernameNormalized).IsUnique();
        builder.Property(u => u.Email).HasMaxLength(254);
        builder.Property(u => u.Username).HasMaxLength(GameRules.UsernameMaxLength);
        builder.Property(u => u.UsernameNormalized).HasMaxLength(GameRules.UsernameMaxLength);
        builder.Property(u => u.Avatar).HasMaxLength(40);
        builder.Property(u => u.Locale).HasMaxLength(5);
        builder.Property(u => u.NotificationsJson).HasColumnName("notifications").HasColumnType("jsonb");
        builder.Property(u => u.EquipmentJson).HasColumnName("equipment").HasColumnType("jsonb");
        builder.HasMany(u => u.Topics).WithOne().HasForeignKey(t => t.UserId);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_users_username_length", $"length(username) between {GameRules.UsernameMinLength} and {GameRules.UsernameMaxLength}");
            t.HasCheckConstraint("ck_users_locale", "locale in ('en', 'hr')");
            t.HasCheckConstraint("ck_users_profile_visibility", $"profile_visibility in ({SnakeCaseEnumConverter<ProfileVisibility>.SqlList()})");
            t.HasCheckConstraint("ck_users_tier", $"tier in ({SnakeCaseEnumConverter<Tier>.SqlList()})");
            t.HasCheckConstraint("ck_users_coins", "coins >= 0");
            t.HasCheckConstraint("ck_users_streak_freezes", $"streak_freezes between 0 and {GameRules.MaxStreakFreezes}");
        });
    }
}

public class UserTopicConfiguration : IEntityTypeConfiguration<UserTopic>
{
    public void Configure(EntityTypeBuilder<UserTopic> builder)
    {
        builder.HasKey(t => new { t.UserId, t.TopicId });
        builder.HasOne<Domain.Content.Topic>().WithMany().HasForeignKey(t => t.TopicId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.FamilyId);
        builder.Property(t => t.TokenHash).HasMaxLength(64);
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
