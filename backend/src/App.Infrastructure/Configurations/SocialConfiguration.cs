using App.Domain.Matches;
using App.Domain.Social;
using App.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Configurations;

public class FriendshipConfiguration : IEntityTypeConfiguration<Friendship>
{
    public void Configure(EntityTypeBuilder<Friendship> builder)
    {
        builder.HasKey(f => new { f.UserLow, f.UserHigh }); // one row per pair, whoever asked
        builder.HasIndex(f => new { f.UserHigh, f.Status });
        builder.HasOne<User>().WithMany().HasForeignKey(f => f.UserLow).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(f => f.UserHigh).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(f => f.RequestedBy).OnDelete(DeleteBehavior.Cascade);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_friendships_order", "user_low < user_high");
            t.HasCheckConstraint("ck_friendships_status", $"status in ({SnakeCaseEnumConverter<FriendshipStatus>.SqlList()})");
        });
    }
}

public class FriendChallengeConfiguration : IEntityTypeConfiguration<FriendChallenge>
{
    public void Configure(EntityTypeBuilder<FriendChallenge> builder)
    {
        builder.HasIndex(c => c.Code).IsUnique();
        builder.Property(c => c.Code).HasMaxLength(6);
        builder.HasIndex(c => c.TargetUserId).HasFilter("match_id is null"); // the inbox
        builder.HasIndex(c => c.HostUserId);
        builder.HasIndex(c => c.GuestUserId);
        builder.HasIndex(c => c.MatchId);
        builder.HasOne<User>().WithMany().HasForeignKey(c => c.HostUserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(c => c.TargetUserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(c => c.GuestUserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Match>().WithMany().HasForeignKey(c => c.MatchId).OnDelete(DeleteBehavior.SetNull);
    }
}
