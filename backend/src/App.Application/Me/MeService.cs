using System.Text.Json;
using App.Application.Content;
using App.Application.Daily;
using App.Application.Economy;
using App.Domain.Economy;
using App.Domain.Matches;
using App.Domain.Progress;
using App.Domain;
using App.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace App.Application.Me;

public class MeService(IAppDbContext db, ContentService content, DailyService daily, TimeProvider time)
{
    public async Task<MeDto> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        await ToDtoAsync(await LoadAsync(userId, cancellationToken), cancellationToken);

    // Slot → owned item id; null takes the slot off, a missing slot stays as it is. Stored as slot → assetKey,
    // which is what every UserRefDto shows. Other keys (titleId arrives with titles in F5) are ignored.
    public async Task<MeDto> SetEquipmentAsync(Guid userId, Dictionary<string, Guid?> slots, CancellationToken cancellationToken)
    {
        var user = await LoadAsync(userId, cancellationToken);
        var equipment = JsonSerializer.Deserialize<Dictionary<string, string>>(user.EquipmentJson) ?? [];
        var known = Enum.GetValues<ShopItemKind>().Select(ShopItem.SlotOf).OfType<string>().ToHashSet();
        foreach (var (slot, itemId) in slots.Where(s => known.Contains(s.Key)))
        {
            if (itemId is not { } id)
            {
                equipment.Remove(slot);
                continue;
            }
            var item = await db.ShopItems.Where(i => i.Id == id && db.UserItems.Any(o => o.UserId == userId && o.ItemId == id)).FirstOrDefaultAsync(cancellationToken);
            if (item is null || ShopItem.SlotOf(item.Kind) != slot)
                throw new AppException(ErrorCodes.ShopNotOwned, "Buy this item in the shop first.");
            equipment[slot] = item.AssetKey;
        }
        user.EquipmentJson = JsonSerializer.Serialize(equipment);
        await db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(user, cancellationToken);
    }

    public async Task<List<RewardEventDto>> GetRewardsAsync(Guid userId, int limit, CancellationToken cancellationToken) =>
        await db.RewardEvents.Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id)
            .Take(Math.Clamp(limit, 1, 100))
            .Select(e => new RewardEventDto(e.Type, e.Coins, e.Xp, e.Sp, e.CreatedAt, e.ReferenceId))
            .ToListAsync(cancellationToken);

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
        return await ToDtoAsync(user, cancellationToken);
    }

    public async Task SetTopicsAsync(Guid userId, Guid[] topicIds, CancellationToken cancellationToken)
    {
        var ids = topicIds.Distinct().ToArray();
        if (ids.Length is < GameRules.MinTopics or > GameRules.MaxTopics)
            throw new AppException(ErrorCodes.TopicsCount, $"Pick between {GameRules.MinTopics} and {GameRules.MaxTopics} topics.");
        if (await db.Topics.CountAsync(t => ids.Contains(t.Id), cancellationToken) != ids.Length)
            throw new AppException(ErrorCodes.TopicNotFound, "One of the topics does not exist.", 404);
        // The queue matched on these topics; changing them means leaving it first (idea §4).
        if (await InQueueAsync(userId, cancellationToken))
            throw new AppException(ErrorCodes.TopicsLockedInQueue, "Leave the queue to change your topics.");

        var user = await LoadAsync(userId, cancellationToken);
        user.Topics.Clear();
        user.Topics.AddRange(ids.Select(id => new UserTopic { UserId = userId, TopicId = id }));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<DashboardDto> GetDashboardAsync(Guid userId, CancellationToken cancellationToken)
    {
        var me = await ToDtoAsync(await LoadAsync(userId, cancellationToken), cancellationToken);
        var topics = await content.ListTopicsAsync(cancellationToken);
        var progress = await TopicProgressAsync(userId, cancellationToken);
        var due = await DueQuery(userId).Select(m => new { m.ConceptId, m.TopicId }).ToListAsync(cancellationToken);
        // Due reviews come first: the topic with the most of them, practiced with exactly those concepts (idea §11).
        var dueTopic = due.GroupBy(d => d.TopicId).OrderByDescending(g => g.Count()).FirstOrDefault();
        var suggestion = dueTopic is not null
            ? new SuggestionDto("review_due", dueTopic.Key, dueTopic.Select(d => d.ConceptId).Take(GameRules.QuestionsPerMatch).ToArray())
            : me.ActiveTopicIds.Length >= GameRules.MinTopics ? new SuggestionDto("play", null, null) : null;
        return new DashboardDto(
            me,
            topics.Select(t =>
            {
                var p = progress.GetValueOrDefault(t.Id);
                return new DashboardTopicDto(t, me.ActiveTopicIds.Contains(t.Id), p?.Mastery, Completion(p?.Graduated ?? 0, t.ConceptCount));
            }).ToArray(),
            await RatingHistoryAsync(userId, cancellationToken),
            due.Count,
            (await daily.TasksAsync(userId, cancellationToken)).Tasks,
            await daily.ChallengeAttemptedTodayAsync(userId, cancellationToken),
            suggestion,
            new InboxDto(await RematchInboxAsync(userId, cancellationToken), await FriendChallengeInboxAsync(userId, cancellationToken)),
            me.InQueue);
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

    // The newest rematch someone asked me for that is still open (20 s).
    private async Task<RematchInboxDto?> RematchInboxAsync(Guid userId, CancellationToken cancellationToken)
    {
        var openSince = time.GetUtcNow().AddSeconds(-GameRules.RematchSeconds);
        var request = await db.MatchPlayers.Where(p => p.UserId == userId && p.Kind == SeatKind.Human)
            .Join(db.Matches, p => p.MatchId, m => m.Id, (p, m) => new { p.Seat, Match = m })
            .Where(x => x.Match.RematchStatus == RematchStatus.Pending && x.Match.RematchRequestedAt > openSince && x.Match.RematchRequestedBySeat != x.Seat)
            .OrderByDescending(x => x.Match.RematchRequestedAt)
            .Select(x => new
            {
                x.Match.Id,
                At = x.Match.RematchRequestedAt!.Value,
                From = db.MatchPlayers.Where(o => o.MatchId == x.Match.Id && o.Seat == x.Match.RematchRequestedBySeat).Select(o => o.User!).First(),
            })
            .FirstOrDefaultAsync(cancellationToken);
        return request is null ? null : new RematchInboxDto(request.Id, UserRefDto.From(request.From), request.At.AddSeconds(GameRules.RematchSeconds));
    }

    // The newest challenge a friend sent me that nobody has joined yet.
    private async Task<FriendChallengeInboxDto?> FriendChallengeInboxAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var challenge = await db.FriendChallenges.AsNoTracking()
            .Where(c => c.TargetUserId == userId && c.MatchId == null && c.ExpiresAt > now)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return challenge is null ? null
            : new FriendChallengeInboxDto(challenge.Code, UserRefDto.From(await db.Users.FirstAsync(u => u.Id == challenge.HostUserId, cancellationToken)), challenge.ExpiresAt);
    }

    // Rating after each of the last 30 ranked and ghost matches, oldest first.
    public async Task<RatingPointDto[]> RatingHistoryAsync(Guid userId, CancellationToken cancellationToken) =>
        (await db.MatchPlayers.Where(p => p.UserId == userId && p.Kind == SeatKind.Human && p.RatingAfter != null)
            .Join(db.Matches, p => p.MatchId, m => m.Id, (p, m) => new { At = m.FinishedAt!.Value, Rating = p.RatingAfter!.Value })
            .OrderByDescending(x => x.At)
            .Take(30)
            .ToListAsync(cancellationToken))
        .OrderBy(x => x.At).Select(x => new RatingPointDto(x.At, x.Rating)).ToArray();

    public record TopicProgress(double? Mastery, int Graduated);

    // Topic mastery averages the answered concepts only: an unknown concept is not a weak one.
    public async Task<Dictionary<Guid, TopicProgress>> TopicProgressAsync(Guid userId, CancellationToken cancellationToken) =>
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

    public static double Completion(int graduated, int concepts) => concepts == 0 ? 0 : 100.0 * graduated / concepts;

    // ponytail: season points are the ledger's SP sum until seasons (F5) give them a season_id and a reset.
    public async Task<MeDto> ToDtoAsync(User user, CancellationToken cancellationToken)
    {
        var self = UserRefDto.From(user);
        var today = Clock.Today(time.GetUtcNow());
        var seasonPoints = await db.RewardEvents.Where(e => e.UserId == user.Id).SumAsync(e => e.Sp, cancellationToken);
        return new(
            self.Id, self.Username, self.Avatar, self.Tier, self.Rating, self.IsProvisional, self.Title, self.Equipment,
            user.Email, user.Locale, user.ProfileVisibility, Notifications(user),
            user.RankedMatches, user.Coins, user.Xp, Level.Of(user.Xp), Level.XpToNext(user.Xp), seasonPoints,
            new StreakDto(Streaks.Current(user, today), user.StreakLastActiveOn, user.StreakFreezes, user.StreakLastActiveOn == today),
            user.Topics.Select(t => t.TopicId).ToArray(), null, user.CurrentMatchId, await InQueueAsync(user.Id, cancellationToken), user.CreatedAt);
    }

    private Task<bool> InQueueAsync(Guid userId, CancellationToken cancellationToken) =>
        db.QueueEntries.AnyAsync(q => q.UserId == userId, cancellationToken);

    private async Task<User> LoadAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Users.Include(u => u.Topics).FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
        ?? throw new AppException(ErrorCodes.AuthRefreshInvalid, "The account no longer exists.", 401);

    private static Dictionary<string, bool> Notifications(User user) =>
        JsonSerializer.Deserialize<Dictionary<string, bool>>(user.NotificationsJson) ?? [];
}
