using App.Domain.Content;
using App.Domain.Progress;
using App.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Configurations;

public class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.HasIndex(q => q.SeedKey).IsUnique();
        builder.HasIndex(q => q.TopicId).HasFilter("status = 'active'"); // the deck builder loads a topic's active pool in one query
        builder.HasIndex(q => q.ConceptId);
        builder.Property(q => q.SeedKey).HasMaxLength(120);
        builder.Property(q => q.Language).HasMaxLength(20);
        builder.Property(q => q.Content).HasColumnType("jsonb");
        builder.Property(q => q.Explanation).HasColumnType("jsonb");
        builder.HasOne<Topic>().WithMany().HasForeignKey(q => q.TopicId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Concept>().WithMany().HasForeignKey(q => q.ConceptId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_questions_type", $"type in ({SnakeCaseEnumConverter<QuestionType>.SqlList()})");
            t.HasCheckConstraint("ck_questions_status", $"status in ({SnakeCaseEnumConverter<QuestionStatus>.SqlList()})");
            t.HasCheckConstraint("ck_questions_difficulty", "difficulty between 1 and 4");
        });
    }
}

public class ConceptMasteryConfiguration : IEntityTypeConfiguration<ConceptMastery>
{
    public void Configure(EntityTypeBuilder<ConceptMastery> builder)
    {
        builder.HasKey(m => new { m.UserId, m.ConceptId });
        builder.Ignore(m => m.State);
        builder.HasIndex(m => new { m.UserId, m.ReviewDueAt }).HasFilter("review_due_at is not null"); // "Due for review: n"
        builder.HasIndex(m => new { m.TopicId, m.UserId }).HasFilter("is_graduated"); // leaderboard per topic
        builder.HasIndex(m => m.ConceptId);
        builder.HasOne<User>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Concept>().WithMany().HasForeignKey(m => m.ConceptId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Topic>().WithMany().HasForeignKey(m => m.TopicId).OnDelete(DeleteBehavior.Cascade);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_concept_mastery_mastery", "mastery between 0 and 100");
            t.HasCheckConstraint("ck_concept_mastery_review_step", "review_step between 1 and 5");
        });
    }
}

public class AnswerConfiguration : IEntityTypeConfiguration<Answer>
{
    public void Configure(EntityTypeBuilder<Answer> builder)
    {
        builder.HasIndex(a => new { a.UserId, a.CreatedAt }).IsDescending(false, true); // seen in the last 30 days, activity per day
        builder.HasIndex(a => new { a.UserId, a.ConceptId, a.CreatedAt }).IsDescending(false, false, true); // last wrong answers per concept
        builder.HasIndex(a => a.QuestionId); // per-question statistics
        builder.HasIndex(a => a.MatchId);
        builder.Property(a => a.Payload).HasColumnType("jsonb");
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Question>().WithMany().HasForeignKey(a => a.QuestionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Concept>().WithMany().HasForeignKey(a => a.ConceptId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Topic>().WithMany().HasForeignKey(a => a.TopicId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Matches.Match>().WithMany().HasForeignKey(a => a.MatchId).OnDelete(DeleteBehavior.SetNull);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_answers_source", $"source in ({SnakeCaseEnumConverter<AnswerSource>.SqlList()})");
            t.HasCheckConstraint("ck_answers_label", $"label in ({SnakeCaseEnumConverter<AnswerLabel>.SqlList()})");
            t.HasCheckConstraint("ck_answers_question_type", $"question_type in ({SnakeCaseEnumConverter<QuestionType>.SqlList()})");
        });
    }
}
