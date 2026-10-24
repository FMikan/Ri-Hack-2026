using App.Domain.Daily;
using App.Domain.Economy;
using App.Domain.Content;
using App.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Configurations;

public class RewardEventConfiguration : IEntityTypeConfiguration<RewardEvent>
{
    public void Configure(EntityTypeBuilder<RewardEvent> builder)
    {
        builder.HasIndex(e => e.DedupeKey).IsUnique(); // Rewarder inserts with "on conflict (dedupe_key) do nothing"
        builder.HasIndex(e => new { e.UserId, e.CreatedAt }).IsDescending(false, true);
        builder.HasIndex(e => new { e.UserId, e.Type, e.CreatedAt }).IsDescending(false, false, true); // daily SP caps, weekly XP goal
        builder.Property(e => e.DedupeKey).HasMaxLength(200);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.ToTable(t => t.HasCheckConstraint("ck_reward_events_type", $"type in ({SnakeCaseEnumConverter<RewardType>.SqlList()})"));
    }
}

public class ActivityDayConfiguration : IEntityTypeConfiguration<ActivityDay>
{
    public void Configure(EntityTypeBuilder<ActivityDay> builder)
    {
        builder.HasKey(d => new { d.UserId, d.Day });
        builder.HasOne<User>().WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserDailyTaskConfiguration : IEntityTypeConfiguration<UserDailyTask>
{
    public void Configure(EntityTypeBuilder<UserDailyTask> builder)
    {
        builder.HasKey(t => new { t.UserId, t.Day, t.TaskKey });
        builder.Property(t => t.TaskKey).HasMaxLength(40);
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Topic>().WithMany().HasForeignKey(t => t.TopicId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class DailyChallengeConfiguration : IEntityTypeConfiguration<DailyChallenge>
{
    public void Configure(EntityTypeBuilder<DailyChallenge> builder)
    {
        builder.HasKey(c => c.Day);
        builder.HasOne<Question>().WithMany().HasForeignKey(c => c.QuestionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class DailyChallengeAttemptConfiguration : IEntityTypeConfiguration<DailyChallengeAttempt>
{
    public void Configure(EntityTypeBuilder<DailyChallengeAttempt> builder)
    {
        builder.HasKey(a => new { a.UserId, a.Day });
        builder.HasIndex(a => new { a.Day, a.IsCorrect, a.ElapsedMs }).IsDescending(false, true, false); // the day's leaderboard
        builder.Property(a => a.Payload).HasColumnType("jsonb");
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<DailyChallenge>().WithMany().HasForeignKey(a => a.Day).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ShopItemConfiguration : IEntityTypeConfiguration<ShopItem>
{
    public void Configure(EntityTypeBuilder<ShopItem> builder)
    {
        builder.HasIndex(i => i.Key).IsUnique();
        builder.Property(i => i.Key).HasMaxLength(80);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_shop_items_kind", $"kind in ({SnakeCaseEnumConverter<ShopItemKind>.SqlList()})");
            t.HasCheckConstraint("ck_shop_items_price", "price > 0");
        });
    }
}

public class UserItemConfiguration : IEntityTypeConfiguration<UserItem>
{
    public void Configure(EntityTypeBuilder<UserItem> builder)
    {
        builder.HasKey(i => new { i.UserId, i.ItemId }); // an item is owned once; ShopService inserts with "on conflict do nothing"
        builder.HasIndex(i => i.ItemId);
        builder.HasOne<User>().WithMany().HasForeignKey(i => i.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ShopItem>().WithMany().HasForeignKey(i => i.ItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
