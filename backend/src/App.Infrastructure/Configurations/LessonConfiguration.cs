using App.Domain.Content;
using App.Domain.Lessons;
using App.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Configurations;

public class LessonConfiguration : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> builder)
    {
        builder.HasIndex(l => new { l.UserId, l.CreatedAt }).IsDescending(false, true);
        builder.HasIndex(l => l.MatchId);
        builder.Property(l => l.Cards).HasColumnType("jsonb");
        builder.Property(l => l.CheckResults).HasColumnType("jsonb");
        builder.HasOne<User>().WithMany().HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Domain.Matches.Match>().WithMany().HasForeignKey(l => l.MatchId).OnDelete(DeleteBehavior.SetNull);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_lessons_status", $"status in ({SnakeCaseEnumConverter<LessonStatus>.SqlList()})");
            t.HasCheckConstraint("ck_lessons_cards_source", $"cards_source in ({SnakeCaseEnumConverter<LessonCardsSource>.SqlList()})");
        });
    }
}

public class ConceptLessonCardsConfiguration : IEntityTypeConfiguration<ConceptLessonCards>
{
    public void Configure(EntityTypeBuilder<ConceptLessonCards> builder)
    {
        builder.HasKey(c => new { c.ConceptId, c.Band });
        builder.Property(c => c.Cards).HasColumnType("jsonb");
        builder.HasOne<Concept>().WithMany().HasForeignKey(c => c.ConceptId).OnDelete(DeleteBehavior.Cascade);
    }
}
