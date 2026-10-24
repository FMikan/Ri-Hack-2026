using System.Net;
using System.Net.Http.Json;
using App.Application.Matches;
using App.Application.Me;
using App.Domain;
using App.Domain.Content;
using App.Domain.Matches;
using App.Domain.Progress;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace App.Api.Tests;

// F2: the review queue, the knowledge map and calibration (idea §11).
public class ProgressTests(TestApp app) : MatchTestBase(app)
{
    private Task<List<Guid>> ConceptIdsAsync(Topic topic) =>
        App.InDbAsync(db => db.Concepts.Where(c => c.TopicId == topic.Id).OrderBy(c => c.SortOrder).Select(c => c.Id).ToListAsync());

    private Task MasteryAsync(Session session, Topic topic, Guid conceptId, double mastery, int answers = 3, DateTimeOffset? dueAt = null, bool graduated = false) =>
        App.InDbAsync(db =>
        {
            db.ConceptMastery.Add(new ConceptMastery
            {
                UserId = session.Auth.User.Id, ConceptId = conceptId, TopicId = topic.Id, Mastery = mastery, AnswerCount = answers,
                ReviewStep = dueAt is null ? null : 1, ReviewDueAt = dueAt, IsGraduated = graduated,
            });
            return db.SaveChangesAsync();
        });

    [Fact]
    public async Task Due_concepts_appear_in_dashboard_and_due_endpoint()
    {
        var topic = await SeedTopicAsync();
        var c = await ConceptIdsAsync(topic);
        var ana = await RegisterAsync("ana");
        var now = App.Clock.GetUtcNow();
        await MasteryAsync(ana, topic, c[0], 60, dueAt: now.AddHours(-2));
        await MasteryAsync(ana, topic, c[1], 30, dueAt: now.AddMinutes(-1));
        await MasteryAsync(ana, topic, c[2], 30, dueAt: now.AddDays(2)); // in the queue, not due yet

        var dashboard = (await ana.Client.GetFromJsonAsync<DashboardDto>("/api/me/dashboard", App.Json))!;
        var due = (await ana.Client.GetFromJsonAsync<DueConceptsDto>("/api/me/progress/due", App.Json))!;

        Assert.Equal(2, dashboard.DueReviews);
        Assert.Equal("review_due", dashboard.Suggestion!.Kind);
        Assert.Equal(topic.Id, dashboard.Suggestion.TopicId);
        Assert.Equal([c[0], c[1]], dashboard.Suggestion.ConceptIds!);
        Assert.Equal([c[0], c[1]], due.Concepts.Select(x => x.Id));
        Assert.All(due.Concepts, x => Assert.Equal(topic.Id, x.Topic.Id));
        Assert.Equal(1, due.Concepts[0].ReviewStep);
    }

    [Fact]
    public async Task Due_concept_answered_correctly_moves_review_step()
    {
        var topic = await SeedTopicAsync();
        var c = await ConceptIdsAsync(topic);
        var ana = await RegisterAsync("ana");
        await MasteryAsync(ana, topic, c[5], 60, dueAt: App.Clock.GetUtcNow().AddMinutes(-1));
        var started = (await ReadAsync<MatchStateDto>(await PostAsync(ana.Client, "/api/matches/practice", new { topicId = topic.Id, timed = false, conceptIds = new[] { c[5] } })))!;

        await AnswerAsync(ana, started, Right(started));

        var concept = await App.InDbAsync(db => db.ConceptMastery.FirstAsync(m => m.UserId == ana.Auth.User.Id && m.ConceptId == c[5]));
        var days = started.Question!.Question.Difficulty == 1 ? 1.5 : 3;
        Assert.Equal(2, concept.ReviewStep);
        Assert.Equal(App.Clock.GetUtcNow().AddDays(days), concept.ReviewDueAt);
    }

    [Fact]
    public async Task Timeout_updates_mastery_as_wrong()
    {
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");
        var question = await FirstQuestionAsync(ana, await StartBotAsync(ana, topic.Id));

        await ToDeadlineAsync(ana, question);

        var answer = await App.InDbAsync(db => db.Answers.SingleAsync(a => a.UserId == ana.Auth.User.Id));
        Assert.Null(answer.Payload);
        Assert.Equal(0, answer.Fraction);
        Assert.True(answer.MasteryAfter < answer.MasteryBefore);
    }

    [Fact]
    public async Task Knowledge_map_shows_states_completion_and_last_answers()
    {
        var topic = await SeedTopicAsync();
        var c = await ConceptIdsAsync(topic);
        var ana = await RegisterAsync("ana");
        var final = await FinishedBotMatchAsync(ana, topic.Id);
        var played = await App.InDbAsync(db => db.MatchQuestions.Where(q => q.MatchId == final.Id).OrderBy(q => q.Index).Select(q => q.ConceptId).ToListAsync());
        var unplayed = c.Except(played).ToList();
        await MasteryAsync(ana, topic, unplayed[0], 90, answers: 12, graduated: true);

        var map = (await ana.Client.GetFromJsonAsync<KnowledgeMapDto>("/api/me/knowledge-map", App.Json))!;

        var entry = Assert.Single(map.Topics);
        Assert.Equal(topic.Id, entry.Topic.Id);
        Assert.False(entry.Active);
        Assert.Equal(100.0 / 12, entry.Completion, 6);
        Assert.True(entry.CalibrationAvailable); // one concept is still unknown
        Assert.Equal(c, entry.Concepts.Select(x => x.Concept.Id));
        Assert.Equal(ConceptState.Graduated, entry.Concepts.Single(x => x.Concept.Id == unplayed[0]).State);
        Assert.Equal(ConceptState.Unknown, entry.Concepts.Single(x => x.Concept.Id == unplayed[1]).State);
        Assert.Empty(entry.Concepts.Single(x => x.Concept.Id == unplayed[1]).LastAnswers);
        var answered = entry.Concepts.Single(x => x.Concept.Id == played[0]);
        var last = Assert.Single(answered.LastAnswers);
        Assert.True(last.IsCorrect); // index 0 was answered right
        Assert.NotEmpty(last.Prompt);
        Assert.NotEqual(ConceptState.Unknown, answered.State);
    }

    [Fact]
    public async Task Topic_mastery_ignores_unknown_concepts()
    {
        var topic = await SeedTopicAsync();
        var c = await ConceptIdsAsync(topic);
        var ana = await RegisterAsync("ana");
        await MasteryAsync(ana, topic, c[0], 80);
        await MasteryAsync(ana, topic, c[1], 60);
        await MasteryAsync(ana, topic, c[2], 50, answers: 0);

        var map = (await ana.Client.GetFromJsonAsync<KnowledgeMapDto>("/api/me/knowledge-map", App.Json))!;
        var dashboard = (await ana.Client.GetFromJsonAsync<DashboardDto>("/api/me/dashboard", App.Json))!;

        Assert.Equal(70, map.Topics[0].Mastery);
        Assert.Equal(70, dashboard.Topics.Single(t => t.Topic.Id == topic.Id).Mastery);
    }

    [Fact]
    public async Task Calibration_uses_5_D1_D2_questions_on_unknown_concepts_without_reward()
    {
        var topic = await SeedTopicAsync();
        var c = await ConceptIdsAsync(topic);
        var ana = await RegisterAsync("ana");
        await MasteryAsync(ana, topic, c[0], 60);

        var response = await PostAsync(ana.Client, "/api/matches/calibration", new { topicId = topic.Id });
        var started = (await ReadAsync<MatchStateDto>(response))!;
        var final = await PlayToEndAsync(ana, await FirstQuestionAsync(ana, started), right: _ => true);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(MatchKind.Calibration, started.Kind);
        Assert.Single(started.Players);
        Assert.Equal(5, final.Result!.Mine.Correct);
        var (answers, me) = await App.InDbAsync(async db => (
            await db.Answers.Where(a => a.UserId == ana.Auth.User.Id).ToListAsync(),
            await db.Users.FirstAsync(u => u.Id == ana.Auth.User.Id)));
        Assert.Equal(5, answers.Count);
        Assert.All(answers, a => Assert.Equal(AnswerSource.Calibration, a.Source));
        Assert.All(answers, a => Assert.InRange(a.Difficulty, 1, 2));
        Assert.DoesNotContain(answers, a => a.ConceptId == c[0]);
        Assert.Equal((0, 0), (me.Coins, me.Xp));
    }

    [Fact]
    public async Task Calibration_without_unknown_concepts_is_409()
    {
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");
        foreach (var concept in await ConceptIdsAsync(topic)) await MasteryAsync(ana, topic, concept, 60);

        var response = await PostAsync(ana.Client, "/api/matches/calibration", new { topicId = topic.Id });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.MatchNothingToCalibrate, (await ReadAsync<ProblemDetails>(response))!.Type);
    }
}
