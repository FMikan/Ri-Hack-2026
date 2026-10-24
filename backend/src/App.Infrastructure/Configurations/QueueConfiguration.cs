using App.Domain.Matches;
using App.Domain.Matchmaking;
using App.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Configurations;

public class QueueEntryConfiguration : IEntityTypeConfiguration<QueueEntry>
{
    public void Configure(EntityTypeBuilder<QueueEntry> builder)
    {
        builder.HasKey(q => q.UserId); // one entry per player
        builder.HasIndex(q => q.ProposalId);
        builder.HasOne<User>().WithMany().HasForeignKey(q => q.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MatchProposal>().WithMany().HasForeignKey(q => q.ProposalId).OnDelete(DeleteBehavior.SetNull);
        builder.ToTable(t => t.HasCheckConstraint("ck_queue_entries_status", $"status in ({SnakeCaseEnumConverter<QueueStatus>.SqlList()})"));
    }
}

public class MatchProposalConfiguration : IEntityTypeConfiguration<MatchProposal>
{
    public void Configure(EntityTypeBuilder<MatchProposal> builder)
    {
        builder.HasIndex(p => p.ExpiresAt).HasFilter("status = 'pending'"); // the tick expires them
        builder.HasIndex(p => p.UserA);
        builder.HasIndex(p => p.UserB);
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.UserA).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.UserB).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Match>().WithMany().HasForeignKey(p => p.MatchId).OnDelete(DeleteBehavior.SetNull);
        builder.ToTable(t => t.HasCheckConstraint("ck_match_proposals_status", $"status in ({SnakeCaseEnumConverter<ProposalStatus>.SqlList()})"));
    }
}
