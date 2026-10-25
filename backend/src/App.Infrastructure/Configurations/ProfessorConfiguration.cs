using App.Domain.Professor;
using App.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Configurations;

public class ProfessorChatConfiguration : IEntityTypeConfiguration<ProfessorChat>
{
    public void Configure(EntityTypeBuilder<ProfessorChat> builder)
    {
        builder.HasIndex(c => new { c.UserId, c.UpdatedAt }).IsDescending(false, true);
        builder.Property(c => c.Title).HasMaxLength(ProfessorRules.TitleMaxLength);
        builder.HasOne<User>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProfessorMessageConfiguration : IEntityTypeConfiguration<ProfessorMessage>
{
    public void Configure(EntityTypeBuilder<ProfessorMessage> builder)
    {
        builder.HasIndex(m => new { m.ChatId, m.CreatedAt });
        builder.Property(m => m.Topic).HasMaxLength(ProfessorRules.TopicMaxLength);
        builder.HasOne<ProfessorChat>().WithMany().HasForeignKey(m => m.ChatId).OnDelete(DeleteBehavior.Cascade);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_professor_messages_role", $"role in ({SnakeCaseEnumConverter<ProfessorRole>.SqlList()})");
            t.HasCheckConstraint("ck_professor_messages_scope", $"scope is null or scope in ({SnakeCaseEnumConverter<ProfessorScope>.SqlList()})");
        });
    }
}

public class ProfessorLessonConfiguration : IEntityTypeConfiguration<ProfessorLesson>
{
    public void Configure(EntityTypeBuilder<ProfessorLesson> builder)
    {
        builder.HasIndex(l => l.MessageId).IsUnique(); // one lesson per answer
        builder.HasIndex(l => l.ChatId);
        builder.Property(l => l.Cards).HasColumnType("jsonb");
        builder.HasOne<User>().WithMany().HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ProfessorChat>().WithMany().HasForeignKey(l => l.ChatId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ProfessorMessage>().WithMany().HasForeignKey(l => l.MessageId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProfessorQuizConfiguration : IEntityTypeConfiguration<ProfessorQuiz>
{
    public void Configure(EntityTypeBuilder<ProfessorQuiz> builder)
    {
        builder.HasIndex(q => q.MessageId).IsUnique(); // one quiz per answer
        builder.HasIndex(q => q.ChatId);
        builder.Property(q => q.Questions).HasColumnType("jsonb");
        builder.Property(q => q.Answers).HasColumnType("jsonb");
        builder.HasOne<User>().WithMany().HasForeignKey(q => q.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ProfessorChat>().WithMany().HasForeignKey(q => q.ChatId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ProfessorMessage>().WithMany().HasForeignKey(q => q.MessageId).OnDelete(DeleteBehavior.Cascade);
    }
}
