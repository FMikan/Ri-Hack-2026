using App.Domain.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Configurations;

public class TopicConfiguration : IEntityTypeConfiguration<Topic>
{
    public void Configure(EntityTypeBuilder<Topic> builder)
    {
        builder.HasIndex(t => t.Slug).IsUnique();
        builder.Property(t => t.Slug).HasMaxLength(60);
        builder.Property(t => t.Name).HasMaxLength(80);
        builder.HasMany(t => t.Concepts).WithOne().HasForeignKey(c => c.TopicId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ConceptConfiguration : IEntityTypeConfiguration<Concept>
{
    public void Configure(EntityTypeBuilder<Concept> builder)
    {
        builder.HasIndex(c => c.Slug).IsUnique();
        builder.Property(c => c.Slug).HasMaxLength(80);
        builder.Property(c => c.Name).HasMaxLength(80);
    }
}
