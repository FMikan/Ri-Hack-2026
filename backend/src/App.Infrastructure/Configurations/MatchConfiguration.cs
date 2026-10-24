using System.Text.Json;
using App.Domain.Content;
using App.Domain.Matches;
using App.Domain.Progress;
using App.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace App.Infrastructure.Configurations;

public class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> builder)
    {
        builder.Ignore(m => m.Source);
        builder.Ignore(m => m.Current);
        builder.HasIndex(m => m.PhaseDeadline).HasFilter("phase <> 'done'"); // the tick
        builder.HasIndex(m => m.FinishedAt).HasFilter("kind in ('ranked', 'ghost')"); // weekly board, ghost candidates
        builder.HasIndex(m => m.TopicId);
        builder.Ignore(m => m.Recording);
        builder.HasOne<Match>().WithMany().HasForeignKey(m => m.RematchOfMatchId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Match>().WithMany().HasForeignKey(m => m.RematchMatchId).OnDelete(DeleteBehavior.SetNull);
        // The veto is one small document per match; compared by its JSON so in-place changes are saved.
        builder.Property(m => m.Veto).HasColumnType("jsonb").HasConversion(
            v => JsonSerializer.Serialize(v, JsonSerializerOptions.Web),
            s => JsonSerializer.Deserialize<VetoState>(s, JsonSerializerOptions.Web),
            new ValueComparer<VetoState?>(
                (a, b) => JsonSerializer.Serialize(a, JsonSerializerOptions.Web) == JsonSerializer.Serialize(b, JsonSerializerOptions.Web),
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Web).GetHashCode(),
                v => JsonSerializer.Deserialize<VetoState>(JsonSerializer.Serialize(v, JsonSerializerOptions.Web), JsonSerializerOptions.Web)));
        builder.HasOne<Topic>().WithMany().HasForeignKey(m => m.TopicId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(m => m.Players).WithOne().HasForeignKey(p => p.MatchId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(m => m.Questions).WithOne().HasForeignKey(q => q.MatchId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(m => m.Answers).WithOne().HasForeignKey(a => a.MatchId).OnDelete(DeleteBehavior.Cascade);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_matches_kind", $"kind in ({SnakeCaseEnumConverter<MatchKind>.SqlList()})");
            t.HasCheckConstraint("ck_matches_phase", $"phase in ({SnakeCaseEnumConverter<MatchPhase>.SqlList()})");
            t.HasCheckConstraint("ck_matches_result_reason", $"result_reason in ({SnakeCaseEnumConverter<MatchResultReason>.SqlList()})");
            t.HasCheckConstraint("ck_matches_rematch_status", $"rematch_status in ({SnakeCaseEnumConverter<RematchStatus>.SqlList()})");
        });
    }
}

public class MatchPlayerConfiguration : IEntityTypeConfiguration<MatchPlayer>
{
    public void Configure(EntityTypeBuilder<MatchPlayer> builder)
    {
        builder.HasKey(p => new { p.MatchId, p.Seat });
        builder.HasIndex(p => new { p.UserId, p.MatchId }); // match history, "same opponent"
        builder.HasOne(p => p.User).WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => p.GhostSourceMatchId);
        builder.HasOne<Match>().WithMany().HasForeignKey(p => p.GhostSourceMatchId).OnDelete(DeleteBehavior.SetNull);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_match_players_seat", "seat in (0, 1)");
            t.HasCheckConstraint("ck_match_players_kind", $"kind in ({SnakeCaseEnumConverter<SeatKind>.SqlList()})");
            t.HasCheckConstraint("ck_match_players_bot_persona", $"bot_persona in ({SnakeCaseEnumConverter<BotPersona>.SqlList()})");
        });
    }
}

public class MatchQuestionConfiguration : IEntityTypeConfiguration<MatchQuestion>
{
    public void Configure(EntityTypeBuilder<MatchQuestion> builder)
    {
        builder.HasKey(q => new { q.MatchId, q.Index });
        builder.HasIndex(q => q.QuestionId);
        builder.HasIndex(q => q.ConceptId);
        builder.HasOne(q => q.Question).WithMany().HasForeignKey(q => q.QuestionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Concept>().WithMany().HasForeignKey(q => q.ConceptId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_match_questions_index", "index between 0 and 9");
            t.HasCheckConstraint("ck_match_questions_slot", $"slot in ({SnakeCaseEnumConverter<MatchSlot>.SqlList()})");
            t.HasCheckConstraint("ck_match_questions_type", $"type in ({SnakeCaseEnumConverter<QuestionType>.SqlList()})");
        });
    }
}

public class MatchAnswerConfiguration : IEntityTypeConfiguration<MatchAnswer>
{
    public void Configure(EntityTypeBuilder<MatchAnswer> builder)
    {
        builder.HasKey(a => new { a.MatchId, a.Seat, a.Index }); // also rejects a second answer to the same question
        builder.Property(a => a.Payload).HasColumnType("jsonb");
        builder.ToTable(t => t.HasCheckConstraint("ck_match_answers_label", $"label in ({SnakeCaseEnumConverter<AnswerLabel>.SqlList()})"));
    }
}
