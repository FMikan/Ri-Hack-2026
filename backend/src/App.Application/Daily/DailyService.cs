using System.Text.Json;
using System.Text.Json.Nodes;
using App.Application.Content;
using App.Application.Economy;
using App.Application.Me;
using App.Domain;
using App.Domain.Content;
using App.Domain.Daily;
using App.Domain.Economy;
using App.Domain.Lessons;
using App.Domain.Matches;
using App.Domain.Progress;
using Microsoft.EntityFrameworkCore;

namespace App.Application.Daily;

// The daily cycle (idea §12, §13): three tasks a day and one find-the-bug challenge for everyone.
public class DailyService(IAppDbContext db, Rewarder rewarder, TimeProvider time)
{
    private const int ChallengeMs = 60_000, LeaderboardSize = 20;

    // Generated on the first request of the day. Progress comes from the day's activity; a task that reaches its target is
    // marked once and pays 20 coins, all three pay 30 more (paid when the player next opens the tasks or the dashboard).
    public async Task<DailyTasksDto> TasksAsync(Guid userId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var day = Clock.Today(now);
        var tasks = await db.UserDailyTasks.Where(t => t.UserId == userId && t.Day == day).ToListAsync(ct);
        if (tasks.Count == 0)
        {
            await GenerateAsync(userId, day, now, ct);
            tasks = await db.UserDailyTasks.Where(t => t.UserId == userId && t.Day == day).ToListAsync(ct);
        }

        var dayStart = Clock.StartOf(day);
        var result = new List<(UserDailyTask Task, int Progress)>();
        foreach (var task in tasks.OrderBy(t => t.TaskKey))
        {
            var progress = Math.Min(task.Target, await ProgressAsync(userId, task, dayStart, now, ct));
            if (task.CompletedAt is null && progress >= task.Target
                && await db.UserDailyTasks.Where(t => t.UserId == userId && t.Day == day && t.TaskKey == task.TaskKey && t.CompletedAt == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.CompletedAt, now), ct) == 1)
            {
                task.CompletedAt = now;
                await rewarder.GrantAsync(userId, new Grant(RewardType.DailyTask, DailyTasks.Reward, 0, 0, null, $"daily_task:{day:yyyy-MM-dd}:{userId}:{task.TaskKey}"), ct);
            }
            result.Add((task, task.CompletedAt is null ? progress : task.Target));
        }
        var allDone = result.All(r => r.Task.CompletedAt is not null);
        if (allDone)
            await rewarder.GrantAsync(userId, new Grant(RewardType.DailyTaskBonus, DailyTasks.Bonus, 0, 0, null, $"daily_task_bonus:{day:yyyy-MM-dd}:{userId}"), ct);

        var topicIds = result.Where(r => r.Task.TopicId is not null).Select(r => r.Task.TopicId!.Value).ToList();
        var topics = await db.Topics.Where(t => topicIds.Contains(t.Id))
            .Select(t => new TopicDto(t.Id, t.Slug, t.Name, t.Description, t.Concepts.Count)).ToDictionaryAsync(t => t.Id, ct);
        return new DailyTasksDto(
            result.Select(r => new DailyTaskDto(r.Task.TaskKey, r.Progress, r.Task.Target, r.Task.CompletedAt is not null, DailyTasks.Reward,
                r.Task.TopicId is { } id ? topics.GetValueOrDefault(id) : null)).ToArray(),
            allDone);
    }

    public Task<bool> ChallengeAttemptedTodayAsync(Guid userId, CancellationToken ct)
    {
        var day = Clock.Today(time.GetUtcNow());
        return db.DailyChallengeAttempts.AnyAsync(a => a.UserId == userId && a.Day == day && a.AnsweredAt != null, ct);
    }

    // The first request of the day opens the attempt: the 60 s run from here, reloads keep the start.
    public async Task<DailyChallengeDto> TodayAsync(Guid userId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var challenge = await ChallengeOfAsync(Clock.Today(now), create: true, ct);
        await db.Database.ExecuteSqlAsync(
            $"insert into daily_challenge_attempts (user_id, day, started_at) values ({userId}, {challenge.Day}, {now}) on conflict do nothing", ct);
        return await MapAsync(userId, challenge, now, ct);
    }

    // The archive: never opens an attempt; key and explanation only for a player who tried that day.
    public async Task<DailyChallengeDto> DayAsync(Guid userId, DateOnly day, CancellationToken ct) =>
        await MapAsync(userId, await ChallengeOfAsync(day, create: false, ct), time.GetUtcNow(), ct);

    public async Task<DailyChallengeDto> AttemptAsync(Guid userId, DailyAttemptRequest request, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var challenge = await ChallengeOfAsync(Clock.Today(now), create: true, ct);
        bool late;
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            await db.Database.ExecuteSqlAsync(
                $"insert into daily_challenge_attempts (user_id, day, started_at) values ({userId}, {challenge.Day}, {now}) on conflict do nothing", ct);
            await db.Database.SqlQuery<int>($"select 1 as \"Value\" from daily_challenge_attempts where user_id = {userId} and day = {challenge.Day} for update").ToListAsync(ct);
            var attempt = await db.DailyChallengeAttempts.FirstAsync(a => a.UserId == userId && a.Day == challenge.Day, ct);
            if (attempt.AnsweredAt is not null)
                throw new AppException(ErrorCodes.DailyAlreadyAttempted, "You already tried today's challenge. Come back tomorrow.");

            var question = await db.Questions.AsNoTracking().FirstAsync(q => q.Id == challenge.QuestionId, ct);
            // Client time within 1.5 s of the server's counts (idea §17); after 60 s + 1.5 s the attempt is a timeout.
            var serverMs = (int)(now - attempt.StartedAt).TotalMilliseconds;
            late = serverMs > ChallengeMs + GameRules.ClientTimeToleranceMs;
            var payload = !late && request.Payload.ValueKind == JsonValueKind.Object ? request.Payload : (JsonElement?)null;
            var fraction = payload is { } p ? Grader.Grade(question.Type, QuestionJson.Answer(question.Content), p) : 0;
            var elapsed = late ? ChallengeMs : Math.Clamp(Math.Abs(serverMs - request.ClientElapsedMs) <= GameRules.ClientTimeToleranceMs ? request.ClientElapsedMs : serverMs, 0, ChallengeMs);
            (attempt.AnsweredAt, attempt.Payload, attempt.IsCorrect, attempt.Fraction, attempt.ElapsedMs) = (now, payload?.GetRawText(), fraction >= 1, fraction, elapsed);

            await db.RecordAsync(new Answer
            {
                UserId = userId, QuestionId = question.Id, ConceptId = question.ConceptId, TopicId = question.TopicId, QuestionType = question.Type,
                Difficulty = question.Difficulty, Source = AnswerSource.DailyChallenge, Payload = attempt.Payload, IsCorrect = fraction >= 1, Fraction = fraction,
                Points = Scoring.Points(GameRules.Types[question.Type].BasePoints, fraction, elapsed, ChallengeMs, timed: true), ElapsedMs = elapsed,
                TimeLimitMs = ChallengeMs, Label = Labels.Of(question.Difficulty, fraction, elapsed, ChallengeMs, late, timed: true), CreatedAt = now,
            }, timed: true, ct);
            // 30 coins for the right answer, 5 for trying (idea §12); 30 XP either way.
            await rewarder.GrantAsync(userId, new Grant(RewardType.DailyChallenge, fraction >= 1 ? 30 : 5, 30, 0, question.Id, $"daily_challenge:{challenge.Day:yyyy-MM-dd}:{userId}"), ct);
            await rewarder.RecordActivityAsync(await db.Users.FirstAsync(u => u.Id == userId, ct), ct);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        if (late)
            throw new AppException(ErrorCodes.DailyTooLate, "Time ran out; the attempt counts as a miss.");
        return await MapAsync(userId, challenge, now, ct);
    }

    private async Task GenerateAsync(Guid userId, DateOnly day, DateTimeOffset now, CancellationToken ct)
    {
        var due = await db.ConceptMastery.CountAsync(m => m.UserId == userId && !m.IsGraduated && m.ReviewDueAt <= now, ct);
        // The weakest of my active topics that has answered concepts.
        var myTopics = db.UserTopics.Where(t => t.UserId == userId).Select(t => t.TopicId);
        var weakest = await db.ConceptMastery.Where(m => m.UserId == userId && m.AnswerCount > 0 && myTopics.Contains(m.TopicId))
            .GroupBy(m => m.TopicId)
            .OrderBy(g => g.Average(m => m.Mastery))
            .Select(g => (Guid?)g.Key)
            .FirstOrDefaultAsync(ct);
        foreach (var rule in DailyTasks.ForDay(day, userId, due, weakest))
        {
            var topic = rule.Key == DailyTasks.PracticeWeakest ? weakest : null;
            await db.Database.ExecuteSqlAsync(
                $"insert into user_daily_tasks (user_id, day, task_key, target, topic_id) values ({userId}, {day}, {rule.Key}, {rule.Target}, {topic}) on conflict do nothing", ct);
        }
    }

    private async Task<int> ProgressAsync(Guid userId, UserDailyTask task, DateTimeOffset dayStart, DateTimeOffset now, CancellationToken ct) => task.TaskKey switch
    {
        DailyTasks.PlayRanked => await FinishedTodayAsync(userId, dayStart, m => m.Kind == MatchKind.Ranked || m.Kind == MatchKind.Ghost, ct),
        DailyTasks.PracticeWeakest => await FinishedTodayAsync(userId, dayStart, m => m.Kind == MatchKind.Practice && m.TopicId == task.TopicId, ct),
        DailyTasks.PatchConcept => await db.Lessons.CountAsync(l => l.UserId == userId && l.Status == LessonStatus.Passed && l.FinishedAt >= dayStart, ct),
        DailyTasks.Challenge => await db.DailyChallengeAttempts.CountAsync(a => a.UserId == userId && a.Day == task.Day && a.AnsweredAt != null, ct),
        DailyTasks.Brilliant => await db.Answers.CountAsync(a => a.UserId == userId && a.CreatedAt >= dayStart && a.Label == AnswerLabel.Brilliant, ct),
        DailyTasks.ReviewAllDue => task.Target - await db.ConceptMastery.CountAsync(m => m.UserId == userId && !m.IsGraduated && m.ReviewDueAt <= now, ct),
        _ => 0,
    };

    private Task<int> FinishedTodayAsync(Guid userId, DateTimeOffset dayStart, System.Linq.Expressions.Expression<Func<Match, bool>> kind, CancellationToken ct) =>
        db.Matches.Where(kind)
            .Where(m => m.FinishedAt >= dayStart && m.ResultReason != MatchResultReason.Aborted && m.Players.Any(p => p.UserId == userId && p.Kind == SeatKind.Human))
            .CountAsync(ct);

    // A find-the-bug question at D2–D3, the same for everyone, chosen from the day (any find-the-bug if none fits).
    private async Task<DailyChallenge> ChallengeOfAsync(DateOnly day, bool create, CancellationToken ct)
    {
        if (await db.DailyChallenges.AsNoTracking().FirstOrDefaultAsync(c => c.Day == day, ct) is { } existing) return existing;
        if (!create) throw new AppException(ErrorCodes.DailyNotFound, "There was no challenge on that day.", 404);

        var candidates = await db.Questions.Where(q => q.Type == QuestionType.FindBug && q.Status == QuestionStatus.Active)
            .OrderBy(q => q.Id)
            .Select(q => new { q.Id, InBand = q.Difficulty == 2 || q.Difficulty == 3 })
            .ToListAsync(ct);
        var pool = candidates.Any(c => c.InBand) ? candidates.Where(c => c.InBand).ToList() : candidates;
        if (pool.Count == 0) throw new AppException(ErrorCodes.DailyNotFound, "There is no challenge today.", 404);
        var questionId = pool[new Random(day.DayNumber).Next(pool.Count)].Id;
        await db.Database.ExecuteSqlAsync($"insert into daily_challenges (day, question_id) values ({day}, {questionId}) on conflict do nothing", ct);
        return await db.DailyChallenges.AsNoTracking().FirstAsync(c => c.Day == day, ct);
    }

    private async Task<DailyChallengeDto> MapAsync(Guid userId, DailyChallenge challenge, DateTimeOffset now, CancellationToken ct)
    {
        var question = await db.Questions.AsNoTracking().FirstAsync(q => q.Id == challenge.QuestionId, ct);
        // Correct first, then the faster time (idea §12).
        var answered = await db.DailyChallengeAttempts.Where(a => a.Day == challenge.Day && a.AnsweredAt != null)
            .OrderByDescending(a => a.IsCorrect).ThenBy(a => a.ElapsedMs).ThenBy(a => a.AnsweredAt)
            .Select(a => new { a.UserId, IsCorrect = a.IsCorrect!.Value, ElapsedMs = a.ElapsedMs!.Value })
            .ToListAsync(ct);
        var top = answered.Take(LeaderboardSize).ToList();
        var topIds = top.Select(a => a.UserId).ToList();
        var users = await db.Users.Where(u => topIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);
        var mine = await db.DailyChallengeAttempts.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == userId && a.Day == challenge.Day, ct);
        var attempted = mine?.AnsweredAt is not null;
        var limit = GameRules.Types[question.Type];

        DailyAttemptDto? myAttempt = null;
        if (mine is { AnsweredAt: not null, Fraction: { } fraction, ElapsedMs: { } elapsed })
        {
            var timedOut = mine.Payload is null;
            myAttempt = new DailyAttemptDto(
                mine.Payload is null ? null : JsonNode.Parse(mine.Payload), fraction >= 1, fraction,
                Scoring.Points(limit.BasePoints, fraction, elapsed, ChallengeMs, timed: true), elapsed, timedOut,
                Labels.Of(question.Difficulty, fraction, elapsed, ChallengeMs, timedOut, timed: true),
                answered.FindIndex(a => a.UserId == userId) + 1);
        }
        return new DailyChallengeDto(
            challenge.Day, QuestionView.Public(question) with { TimeLimitMs = ChallengeMs }, mine?.StartedAt, now, attempted, myAttempt,
            attempted ? QuestionView.Key(question) : null,
            attempted ? QuestionView.Explanation(question.Explanation) : null,
            top.Select((a, i) => new DailyLeaderboardEntryDto(i + 1, UserRefDto.From(users[a.UserId]), a.IsCorrect, a.ElapsedMs)).ToArray());
    }
}
