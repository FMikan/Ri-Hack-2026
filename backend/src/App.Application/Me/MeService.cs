using System.Text.Json;
using App.Application.Content;
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
        // ponytail: mastery, completion, history, reviews, tasks and inbox stay empty until F1–F5 add the tables behind them.
        return new DashboardDto(
            me,
            topics.Select(t => new DashboardTopicDto(t, me.ActiveTopicIds.Contains(t.Id), null, 0)).ToArray(),
            [],
            0,
            [],
            false,
            me.ActiveTopicIds.Length >= GameRules.MinTopics ? new SuggestionDto("play", null, null) : null,
            new InboxDto(null, null),
            false);
    }

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
