using App.Domain.Seasons;
using App.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Configurations;

public class SeasonConfiguration : IEntityTypeConfiguration<Season>
{
    public void Configure(EntityTypeBuilder<Season> builder) =>
        builder.HasIndex(s => s.Number).IsUnique(); // SeasonOps inserts the next one with "on conflict (number) do nothing"
}

public class UserSeasonConfiguration : IEntityTypeConfiguration<UserSeason>
{
    public void Configure(EntityTypeBuilder<UserSeason> builder)
    {
        builder.HasKey(s => new { s.UserId, s.SeasonId });
        builder.HasIndex(s => new { s.SeasonId, s.Sp }).IsDescending(false, true); // the season board
        builder.HasOne<User>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Season>().WithMany().HasForeignKey(s => s.SeasonId).OnDelete(DeleteBehavior.Cascade);
        builder.ToTable(t => t.HasCheckConstraint("ck_user_seasons_best_tier", $"best_tier in ({SnakeCaseEnumConverter<Tier>.SqlList()})"));
    }
}

public class TitleConfiguration : IEntityTypeConfiguration<Title>
{
    public void Configure(EntityTypeBuilder<Title> builder)
    {
        builder.HasIndex(t => t.Key).IsUnique();
        builder.Property(t => t.Key).HasMaxLength(80);
        builder.ToTable(t => t.HasCheckConstraint("ck_titles_kind", $"kind in ({SnakeCaseEnumConverter<TitleKind>.SqlList()})"));
    }
}

public class UserTitleConfiguration : IEntityTypeConfiguration<UserTitle>
{
    public void Configure(EntityTypeBuilder<UserTitle> builder)
    {
        builder.HasKey(t => new { t.UserId, t.TitleId });
        builder.HasIndex(t => t.TitleId);
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Title>().WithMany().HasForeignKey(t => t.TitleId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserAchievementConfiguration : IEntityTypeConfiguration<UserAchievement>
{
    public void Configure(EntityTypeBuilder<UserAchievement> builder)
    {
        builder.HasKey(a => new { a.UserId, a.Key });
        builder.Property(a => a.Key).HasMaxLength(40);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class WeeklyResultConfiguration : IEntityTypeConfiguration<WeeklyResult>
{
    public void Configure(EntityTypeBuilder<WeeklyResult> builder)
    {
        builder.HasKey(r => new { r.WeekStart, r.UserId });
        builder.HasIndex(r => r.UserId);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
