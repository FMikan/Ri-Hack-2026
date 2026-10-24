using App.Domain.Content;
using App.Domain.Lessons;
using App.Domain.Matches;
using App.Domain.Matchmaking;
using App.Domain.Progress;
using App.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace App.Application;

// Implemented by App.Infrastructure.AppDbContext, so use cases never reference Infrastructure.
public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<PasswordResetToken> PasswordResetTokens { get; }
    DbSet<UserTopic> UserTopics { get; }
    DbSet<Topic> Topics { get; }
    DbSet<Concept> Concepts { get; }
    DbSet<Question> Questions { get; }
    DbSet<QuestionReport> QuestionReports { get; }
    DbSet<ConceptMastery> ConceptMastery { get; }
    DbSet<Answer> Answers { get; }
    DbSet<Match> Matches { get; }
    DbSet<MatchPlayer> MatchPlayers { get; }
    DbSet<MatchQuestion> MatchQuestions { get; }
    DbSet<MatchAnswer> MatchAnswers { get; }
    DbSet<Lesson> Lessons { get; }
    DbSet<QueueEntry> QueueEntries { get; }
    DbSet<MatchProposal> MatchProposals { get; }
    DbSet<ConceptLessonCards> ConceptLessonCards { get; }

    DatabaseFacade Database { get; } // transactions and row locks (SELECT ... FOR UPDATE)

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
