using App.Domain.Daily;
using App.Domain.Economy;
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
