using System.Net;
using System.Net.Http.Json;
using App.Application.Daily;
using App.Application.Economy;
using App.Application.Lessons;
using App.Application.Me;
using App.Domain;
using App.Domain.Content;
using App.Domain.Daily;
using App.Domain.Economy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace App.Api.Tests;

// F4b: daily tasks and the daily challenge (idea §12, §13).
public class DailyTests(TestApp app) : MatchTestBase(app)
{
    private static readonly object FindBugRight = new { line = 3, fixId = "f2" };
    private static readonly object FindBugWrong = new { line = 1, fixId = "f1" };

    private Task<DailyChallengeDto> ChallengeAsync(Session session) =>
        session.Client.GetFromJsonAsync<DailyChallengeDto>("/api/daily-challenge", App.Json)!;

    private Task<HttpResponseMessage> AttemptAsync(Session session, object payload, int elapsedMs = 500) =>
        PostAsync(session.Client, "/api/daily-challenge/attempt", new { payload, clientElapsedMs = elapsedMs });

    private Task<DailyTasksDto> TasksAsync(Session session) =>
        session.Client.GetFromJsonAsync<DailyTasksDto>("/api/me/daily-tasks", App.Json)!;

    private Task<int> CoinsAsync(Session session) => App.InDbAsync(db => db.Users.Where(u => u.Id == session.Auth.User.Id).Select(u => u.Coins).FirstAsync());

    [Fact]
    public async Task Daily_tasks_are_three_and_the_same_all_day()
    {
        var ana = await RegisterAsync("ana");

        var first = await TasksAsync(ana);
        var later = await TasksAsync(ana);

        Assert.Equal(3, first.Tasks.Length);
        Assert.Equal(first.Tasks.Select(t => t.Key), later.Tasks.Select(t => t.Key));
        Assert.All(first.Tasks, t => Assert.Equal((0, false, 20), (t.Progress, t.Completed, t.Reward)));
        Assert.DoesNotContain(first.Tasks, t => t.Key is DailyTasks.ReviewAllDue or DailyTasks.PracticeWeakest); // nothing due, no answers yet
        Assert.False(first.BonusAwarded);
        Assert.Equal(3, (await ana.Client.GetFromJsonAsync<DashboardDto>("/api/me/dashboard", App.Json))!.DailyTasks.Length);
    }

    [Fact]
    public async Task Completing_all_three_gives_20_each_and_a_bonus_30()
    {
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");
        var today = Clock.Today(App.Clock.GetUtcNow());
        await App.InDbAsync(db =>
        {
            db.UserDailyTasks.AddRange(
                new UserDailyTask { UserId = ana.Auth.User.Id, Day = today, TaskKey = DailyTasks.PatchConcept, Target = 1 },
                new UserDailyTask { UserId = ana.Auth.User.Id, Day = today, TaskKey = DailyTasks.Challenge, Target = 1 },
                new UserDailyTask { UserId = ana.Auth.User.Id, Day = today, TaskKey = DailyTasks.PracticeWeakest, Target = 1, TopicId = topic.Id });
            return db.SaveChangesAsync();
        });

        var match = await FinishedBotMatchAsync(ana, topic.Id);
        var lesson = (await ReadAsync<LessonDto>(await PostAsync(ana.Client, "/api/lessons", new StartLessonRequest { MatchId = match.Id })))!;
        await PostAsync(ana.Client, $"/api/lessons/{lesson.Id}/check", new
        {
            answers = lesson.Check.Select(c => new { questionId = c.Question.Id, payload = QuestionFixtures.RightAnswer(c.Question.Type) }),
        });
        var halfway = await TasksAsync(ana);
        var practice = (await ReadAsync<App.Application.Matches.MatchStateDto>(await PostAsync(ana.Client, "/api/matches/practice", new { topicId = topic.Id, timed = true })))!;
        await PlayToEndAsync(ana, await FirstQuestionAsync(ana, practice));
        await ChallengeAsync(ana);
        await AttemptAsync(ana, FindBugWrong);
        var coinsBefore = await CoinsAsync(ana);

        var done = await TasksAsync(ana);
        var coinsAfter = await CoinsAsync(ana);
        await TasksAsync(ana);

        bool Completed(DailyTasksDto tasks, string key) => tasks.Tasks.Single(t => t.Key == key).Completed;
        Assert.True(Completed(halfway, DailyTasks.PatchConcept));
        Assert.False(Completed(halfway, DailyTasks.Challenge));
        Assert.All(done.Tasks, t => Assert.True(t.Completed));
        Assert.Equal(topic.Id, done.Tasks.Single(t => t.Key == DailyTasks.PracticeWeakest).Topic!.Id);
        Assert.True(done.BonusAwarded);
        Assert.Equal(coinsBefore + 2 * 20 + 30, coinsAfter); // the lesson task was already paid when it completed halfway
        Assert.Equal(coinsAfter, await CoinsAsync(ana)); // completion pays once
    }

    [Fact]
    public async Task Daily_challenge_first_get_starts_attempt()
    {
        await SeedTopicAsync();
        var ana = await RegisterAsync("ana");

        var challenge = await ChallengeAsync(ana);

        Assert.Equal(QuestionType.FindBug, challenge.Question.Type);
        Assert.InRange(challenge.Question.Difficulty, 2, 3);
        Assert.Equal(60_000, challenge.Question.TimeLimitMs);
        Assert.Equal(App.Clock.GetUtcNow(), challenge.StartedAt);
        Assert.False(challenge.Attempted);
        Assert.Null(challenge.Key);
        App.Clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(challenge.StartedAt, (await ChallengeAsync(ana)).StartedAt); // a reload keeps the original start
        Assert.Equal(challenge.Question.Id, (await ChallengeAsync(await RegisterAsync("bob"))).Question.Id); // the same for everyone
    }

    [Fact]
    public async Task Daily_challenge_rewards_30_correct_5_attempt_once()
    {
        await SeedTopicAsync();
        var ana = await RegisterAsync("ana");
        var bob = await RegisterAsync("bob");
        await ChallengeAsync(ana);
        await ChallengeAsync(bob);

        var right = (await ReadAsync<DailyChallengeDto>(await AttemptAsync(ana, FindBugRight)))!;
        var wrong = (await ReadAsync<DailyChallengeDto>(await AttemptAsync(bob, FindBugWrong)))!;
        var twice = await AttemptAsync(ana, FindBugRight);

        Assert.True(right.MyAttempt!.IsCorrect);
        Assert.NotNull(right.Key);
        Assert.False(wrong.MyAttempt!.IsCorrect);
        Assert.Equal((30, 5), (await CoinsAsync(ana), await CoinsAsync(bob)));
        Assert.Equal(ErrorCodes.DailyAlreadyAttempted, (await ReadAsync<ProblemDetails>(twice))!.Type);
        var me = (await ana.Client.GetFromJsonAsync<MeDto>("/api/me", App.Json))!;
        Assert.True(me.Streak.SecuredToday);
        Assert.True((await ana.Client.GetFromJsonAsync<DashboardDto>("/api/me/dashboard", App.Json))!.DailyChallengeAttempted);
    }

    [Fact]
    public async Task Daily_challenge_rejects_answer_after_60s()
    {
        await SeedTopicAsync();
        var ana = await RegisterAsync("ana");
        await ChallengeAsync(ana);

        App.Clock.Advance(TimeSpan.FromSeconds(62));
        var late = await AttemptAsync(ana, FindBugRight);
        var after = await ChallengeAsync(ana);

        Assert.Equal(ErrorCodes.DailyTooLate, (await ReadAsync<ProblemDetails>(late))!.Type);
        Assert.True(after.Attempted);
        Assert.True(after.MyAttempt!.TimedOut);
        Assert.Equal(5, await CoinsAsync(ana)); // it still counts as an attempt
    }

    [Fact]
    public async Task Daily_challenge_leaderboard_orders_correct_then_time()
    {
        await SeedTopicAsync();
        var players = new List<Session>();
        foreach (var (name, payload, ms) in new[] { ("slowright", FindBugRight, 9000), ("wrongfast", FindBugWrong, 300), ("fastright", FindBugRight, 2000) })
        {
            var session = await RegisterAsync(name);
            await ChallengeAsync(session);
            App.Clock.Advance(TimeSpan.FromMilliseconds(ms));
            await AttemptAsync(session, payload, ms);
            players.Add(session);
        }

        var board = (await ChallengeAsync(players[0])).Leaderboard;

        Assert.Equal(["fastright", "slowright", "wrongfast"], board.Select(e => e.User.Username));
        Assert.Equal([1, 2, 3], board.Select(e => e.Rank));
        Assert.Equal(2, (await ChallengeAsync(players[0])).MyAttempt!.Rank);
    }

    [Fact]
    public async Task Daily_challenge_archive_shows_key_only_after_attempt()
    {
        await SeedTopicAsync();
        var ana = await RegisterAsync("ana");
        var bob = await RegisterAsync("bob");
        var day = (await ChallengeAsync(ana)).Day;
        await AttemptAsync(ana, FindBugRight);

        var theirs = await bob.Client.GetFromJsonAsync<DailyChallengeDto>($"/api/daily-challenge/{day:yyyy-MM-dd}", App.Json);
        var mine = await ana.Client.GetFromJsonAsync<DailyChallengeDto>($"/api/daily-challenge/{day:yyyy-MM-dd}", App.Json);

        Assert.Null(theirs!.Key);
        Assert.Null(theirs.StartedAt); // the archive never opens an attempt
        Assert.NotNull(mine!.Key);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.Client.GetAsync("/api/daily-challenge/2020-01-01")).StatusCode);
    }
}
