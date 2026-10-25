using App.Domain.Content;
using App.Domain.Daily;
using App.Domain.Economy;
using App.Domain.Lessons;
using App.Domain.Matches;
using App.Domain.Matchmaking;
using App.Domain.Professor;
using App.Domain.Progress;
using App.Domain.Seasons;
using App.Domain.Social;
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
    DbSet<GhostOffer> GhostOffers { get; }
    DbSet<RewardEvent> RewardEvents { get; }
    DbSet<ActivityDay> ActivityDays { get; }
    DbSet<UserDailyTask> UserDailyTasks { get; }
    DbSet<DailyChallenge> DailyChallenges { get; }
    DbSet<DailyChallengeAttempt> DailyChallengeAttempts { get; }
    DbSet<ShopItem> ShopItems { get; }
    DbSet<UserItem> UserItems { get; }
    DbSet<ConceptLessonCards> ConceptLessonCards { get; }
    DbSet<Friendship> Friendships { get; }
    DbSet<FriendChallenge> FriendChallenges { get; }
    DbSet<Season> Seasons { get; }
    DbSet<UserSeason> UserSeasons { get; }
    DbSet<Title> Titles { get; }
    DbSet<UserTitle> UserTitles { get; }
    DbSet<UserAchievement> UserAchievements { get; }
    DbSet<WeeklyResult> WeeklyResults { get; }
    DbSet<ProfessorChat> ProfessorChats { get; }
    DbSet<ProfessorMessage> ProfessorMessages { get; }
    DbSet<ProfessorLesson> ProfessorLessons { get; }
    DbSet<ProfessorQuiz> ProfessorQuizzes { get; }

    DatabaseFacade Database { get; } // transactions and row locks (SELECT ... FOR UPDATE)

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
