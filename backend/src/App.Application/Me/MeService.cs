using System.Text.Json;
using App.Application.Content;
using App.Domain.Progress;
using App.Domain;
using App.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace App.Application.Me;

public class MeService(IAppDbContext db, ContentService content, TimeProvider time)
{
    public async Task<MeDto> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        ToDto(await LoadAsync(userId, cancellationToken));

    public async Task<MeDto> UpdateAsync(Guid userId, UpdateMeRequest request, CancellationToken cancellationToken)
    {
        var user = await LoadAsync(userId, cancellationToken);
        if (request.Username is { } username && User.Normalize(username) != user.UsernameNormalized)
        {
            var normalized = User.Normalize(username);
            if (await db.Users.AnyAsync(u => u.UsernameNormalized == normalized, cancellationToken))
                throw new AppException(ErrorCodes.AuthUsernameTaken, "This username is taken.");
            user.UsernameNormalized = normalized;
        }
        if (request.Username is not null) user.Username = request.Username;
        if (request.Avatar is not null) user.Avatar = request.Avatar;
        if (request.Locale is not null) user.Locale = request.Locale;
        if (request.ProfileVisibility is { } visibility) user.ProfileVisibility = visibility;
        if (request.Notifications is not null)
        {
            var notifications = Notifications(user);
            foreach (var (key, enabled) in request.Notifications.Where(n => notifications.ContainsKey(n.Key)))
                notifications[key] = enabled;
            user.NotificationsJson = JsonSerializer.Serialize(notifications);
        }
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(user);
    }

    public async Task SetTopicsAsync(Guid userId, Guid[] topicIds, CancellationToken cancellationToken)
    {
        var ids = topicIds.Distinct().ToArray();
        if (ids.Length is < GameRules.MinTopics or > GameRules.MaxTopics)
            throw new AppException(ErrorCodes.TopicsCount, $"Pick between {GameRules.MinTopics} and {GameRules.MaxTopics} topics.");
        if (await db.Topics.CountAsync(t => ids.Contains(t.Id), cancellationToken) != ids.Length)
            throw new AppException(ErrorCodes.TopicNotFound, "One of the topics does not exist.", 404);

        var user = await LoadAsync(userId, cancellationToken);
        user.Topics.Clear();
        user.Topics.AddRange(ids.Select(id => new UserTopic { UserId = userId, TopicId = id }));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<DashboardDto> GetDashboardAsync(Guid userId, CancellationToken cancellationToken)
    {
        var me = ToDto(await LoadAsync(userId, cancellationToken));
        var topics = await content.ListTopicsAsync(cancellationToken);
        var progress = await TopicProgressAsync(userId, cancellationToken);
        var due = await DueQuery(userId).Select(m => new { m.ConceptId, m.TopicId }).ToListAsync(cancellationToken);
        // Due reviews come first: the topic with the most of them, practiced with exactly those concepts (idea §11).
        var dueTopic = due.GroupBy(d => d.TopicId).OrderByDescending(g => g.Count()).FirstOrDefault();
        var suggestion = dueTopic is not null
            ? new SuggestionDto("review_due", dueTopic.Key, dueTopic.Select(d => d.ConceptId).Take(GameRules.QuestionsPerMatch).ToArray())
            : me.ActiveTopicIds.Length >= GameRules.MinTopics ? new SuggestionDto("play", null, null) : null;
        // ponytail: rating history, daily tasks and the inbox stay empty until F3–F5 add the tables behind them.
        return new DashboardDto(
            me,
            topics.Select(t =>
            {
                var p = progress.GetValueOrDefault(t.Id);
                return new DashboardTopicDto(t, me.ActiveTopicIds.Contains(t.Id), p?.Mastery, Completion(p?.Graduated ?? 0, t.ConceptCount));
            }).ToArray(),
            [],
            due.Count,
            [],
            false,
            suggestion,
            new InboxDto(null, null),
            false);
    }

    public async Task<DueConceptsDto> GetDueAsync(Guid userId, CancellationToken cancellationToken)
    {
        var topics = (await content.ListTopicsAsync(cancellationToken)).ToDictionary(t => t.Id);
        var due = await DueQuery(userId)
            .Join(db.Concepts, m => m.ConceptId, c => c.Id, (m, c) => new { c.Id, c.Slug, c.Name, c.TopicId, DueAt = m.ReviewDueAt!.Value, Step = m.ReviewStep!.Value })
            .OrderBy(d => d.DueAt)
            .ToListAsync(cancellationToken);
        return new DueConceptsDto(due
            .Where(d => topics.ContainsKey(d.TopicId))
            .Select(d => new DueConceptDto(d.Id, d.Slug, d.Name, d.TopicId, d.DueAt, d.Step, topics[d.TopicId]))
            .ToArray());
    }

    // Every playable topic with each concept's state and the player's last three answers on it (idea §11).
    public async Task<KnowledgeMapDto> GetKnowledgeMapAsync(Guid userId, CancellationToken cancellationToken)
    {
        var topics = await content.ListTopicsAsync(cancellationToken);
        var topicIds = topics.Select(t => t.Id).ToList();
        var active = (await db.UserTopics.Where(t => t.UserId == userId).Select(t => t.TopicId).ToListAsync(cancellationToken)).ToHashSet();
        var concepts = await db.Concepts.Where(c => topicIds.Contains(c.TopicId)).OrderBy(c => c.SortOrder).ToListAsync(cancellationToken);
        var mastery = await db.ConceptMastery.AsNoTracking().Where(m => m.UserId == userId).ToDictionaryAsync(m => m.ConceptId, cancellationToken);
        // A lateral join: the last three answers of every concept in one query.
        var last = await db.Concepts.Where(c => topicIds.Contains(c.TopicId))
            .SelectMany(c => db.Answers.Where(a => a.UserId == userId && a.ConceptId == c.Id).OrderByDescending(a => a.CreatedAt).Take(3))
            .Select(a => new { a.ConceptId, a.QuestionId, a.QuestionType, a.IsCorrect, a.CreatedAt })
            .ToListAsync(cancellationToken);
        var questionIds = last.Select(a => a.QuestionId).Distinct().ToList();
        var prompts = (await db.Questions.Where(q => questionIds.Contains(q.Id)).Select(q => new { q.Id, q.Content }).ToListAsync(cancellationToken))
            .ToDictionary(q => q.Id, q => QuestionView.Prompt(q.Content));
        var lastByConcept = last.ToLookup(a => a.ConceptId);

        return new KnowledgeMapDto(topics.Select(topic =>
        {
            var rows = concepts.Where(c => c.TopicId == topic.Id).ToList();
            var answered = rows.Select(c => mastery.GetValueOrDefault(c.Id)).Where(m => m is { AnswerCount: > 0 }).ToList();
            return new KnowledgeTopicDto(
                topic,
                active.Contains(topic.Id),
                answered.Count > 0 ? answered.Average(m => m!.Mastery) : null,
                Completion(answered.Count(m => m!.IsGraduated), rows.Count),
                answered.Count > 0 && answered.Count < rows.Count, // offered after the first match, while something is still unknown
                rows.Select(c =>
                {
                    var m = mastery.GetValueOrDefault(c.Id);
                    return new KnowledgeConceptDto(
                        new ConceptDto(c.Id, c.Slug, c.Name, c.TopicId), m?.Mastery ?? GameRules.MasteryStart, ConceptStates.Of(m), m?.ReviewDueAt, m?.ReviewStep,
                        lastByConcept[c.Id].OrderByDescending(a => a.CreatedAt)
                            .Select(a => new LastAnswerDto(a.QuestionId, a.QuestionType, prompts[a.QuestionId], a.IsCorrect, a.CreatedAt)).ToArray());
                }).ToArray());
        }).ToArray());
    }

    private record TopicProgress(double? Mastery, int Graduated);

    // Topic mastery averages the answered concepts only: an unknown concept is not a weak one.
    private async Task<Dictionary<Guid, TopicProgress>> TopicProgressAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.ConceptMastery.Where(m => m.UserId == userId)
            .GroupBy(m => m.TopicId)
            .Select(g => new
            {
                g.Key,
                Mastery = g.Where(m => m.AnswerCount > 0).Average(m => (double?)m.Mastery),
                Graduated = g.Count(m => m.IsGraduated),
            })
            .ToDictionaryAsync(x => x.Key, x => new TopicProgress(x.Mastery, x.Graduated), cancellationToken);

    private IQueryable<ConceptMastery> DueQuery(Guid userId)
    {
        var now = time.GetUtcNow();
        return db.ConceptMastery.Where(m => m.UserId == userId && !m.IsGraduated && m.ReviewDueAt <= now).OrderBy(m => m.ReviewDueAt);
    }

    private static double Completion(int graduated, int concepts) => concepts == 0 ? 0 : 100.0 * graduated / concepts;

    public MeDto ToDto(User user)
    {
        var self = UserRefDto.From(user);
        return new(
            self.Id, self.Username, self.Avatar, self.Tier, self.Rating, self.IsProvisional, self.Title, self.Equipment,
            user.Email, user.Locale, user.ProfileVisibility, Notifications(user),
            user.RankedMatches, user.Coins, user.Xp, Level.Of(user.Xp), Level.XpToNext(user.Xp), 0,
            new StreakDto(user.StreakCount, user.StreakLastActiveOn, user.StreakFreezes, user.StreakLastActiveOn == Clock.Today(time.GetUtcNow())),
            user.Topics.Select(t => t.TopicId).ToArray(), null, user.CurrentMatchId, false, user.CreatedAt);
    }

    private async Task<User> LoadAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Users.Include(u => u.Topics).FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
        ?? throw new AppException(ErrorCodes.AuthRefreshInvalid, "The account no longer exists.", 401);

    private static Dictionary<string, bool> Notifications(User user) =>
        JsonSerializer.Deserialize<Dictionary<string, bool>>(user.NotificationsJson) ?? [];
}
