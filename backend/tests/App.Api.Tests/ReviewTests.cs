using System.Net;
using System.Net.Http.Json;
using App.Application.Content;
using App.Application.Matches;
using App.Domain;
using App.Domain.Content;
using App.Domain.Progress;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace App.Api.Tests;

// F2: the detailed analysis after a match, explanations, the question view and reports.
public class ReviewTests(TestApp app) : MatchTestBase(app)
{
    private Task<MatchReviewDto> ReviewAsync(Session session, Guid matchId) =>
        session.Client.GetFromJsonAsync<MatchReviewDto>($"/api/matches/{matchId}/review", App.Json)!;

    [Fact]
    public async Task Review_lists_10_questions_with_labels_and_mastery_delta()
    {
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");
        var final = await FinishedBotMatchAsync(ana, topic.Id);

        var review = await ReviewAsync(ana, final.Id);

        Assert.Equal(final.Id, review.Match.Id);
        Assert.Equal(Enumerable.Range(0, 10), review.Questions.Select(q => q.Index));
        Assert.All(review.Questions, q => Assert.NotNull(q.Opponent));
        Assert.All(review.Questions, q => Assert.Equal(q.Index % 2 == 0, q.Mine.IsCorrect));
        Assert.All(review.Questions.Where(q => !q.Mine.IsCorrect), q => Assert.Contains(q.Mine.Label, new[] { AnswerLabel.Blunder, AnswerLabel.Miss }));
        Assert.Equal(10, review.MasteryDelta.Length);
        var rightConcepts = review.Questions.Where(q => q.Mine.IsCorrect).Select(q => q.Concept.Id).ToHashSet();
        Assert.All(review.MasteryDelta, d => Assert.Equal(rightConcepts.Contains(d.Concept.Id), d.After > d.Before));
        Assert.True(final.Result!.ReviewAvailable);
    }

    [Fact]
    public async Task Review_before_finish_is_409()
    {
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");
        var started = await StartBotAsync(ana, topic.Id);

        var response = await ana.Client.GetAsync($"/api/matches/{started.Id}/review");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.MatchNotFinished, (await ReadAsync<ProblemDetails>(response))!.Type);
    }

    [Fact]
    public async Task Explanation_is_generated_once_and_cached()
    {
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");
        var final = await FinishedBotMatchAsync(ana, topic.Id);
        var questionId = (await ReviewAsync(ana, final.Id)).Questions[1].Question.Id;

        var first = await ana.Client.GetFromJsonAsync<ExplanationDto>($"/api/questions/{questionId}/explanation", App.Json);
        var second = await ana.Client.GetFromJsonAsync<ExplanationDto>($"/api/questions/{questionId}/explanation", App.Json);

        Assert.Equal(1, App.Ai.ExplanationCalls);
        Assert.Equal(new ExplanationDto("AI why correct", "AI why distractor", "AI remember", "ai"), first);
        Assert.Equal(first, second);
        Assert.Equal(first, (await ReviewAsync(ana, final.Id)).Questions[1].Explanation);
    }

    [Fact]
    public async Task Explanation_falls_back_to_remember_when_generator_times_out()
    {
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");
        var final = await FinishedBotMatchAsync(ana, topic.Id);
        var question = (await ReviewAsync(ana, final.Id)).Questions[1].Question;
        App.Ai.Delay = TimeSpan.FromSeconds(5);

        var explanation = await ana.Client.GetFromJsonAsync<ExplanationDto>($"/api/questions/{question.Id}/explanation", App.Json);

        Assert.Equal("static", explanation!.Source);
        Assert.Equal($"Remember {question.Type}", explanation.Remember);
        Assert.Null(await App.InDbAsync(db => db.Questions.Where(q => q.Id == question.Id).Select(q => q.Explanation).FirstAsync()));
    }

    [Fact]
    public async Task Unanswered_question_shows_no_key_and_no_explanation()
    {
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");
        var bob = await RegisterAsync("bob");
        var final = await FinishedBotMatchAsync(ana, topic.Id);
        var questionId = (await ReviewAsync(ana, final.Id)).Questions[0].Question.Id;

        var mine = await ana.Client.GetFromJsonAsync<QuestionDetailDto>($"/api/questions/{questionId}", App.Json);
        var theirs = await bob.Client.GetFromJsonAsync<QuestionDetailDto>($"/api/questions/{questionId}", App.Json);
        var explanation = await bob.Client.GetAsync($"/api/questions/{questionId}/explanation");

        Assert.NotNull(mine!.Key);
        Assert.Null(theirs!.Key);
        Assert.False(theirs.Content.ContainsKey("answer"));
        Assert.Equal(HttpStatusCode.Forbidden, explanation.StatusCode);
        Assert.Equal(ErrorCodes.QuestionNotAnswered, (await ReadAsync<ProblemDetails>(explanation))!.Type);
        Assert.Equal(0, App.Ai.ExplanationCalls);
    }

    [Fact]
    public async Task Report_question_three_times_quarantines_it_and_keeps_match_result()
    {
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");
        var bob = await RegisterAsync("bob");
        var cid = await RegisterAsync("cid");
        var final = await FinishedBotMatchAsync(ana, topic.Id);
        var question = (await ReviewAsync(ana, final.Id)).Questions[3].Question;
        await AnsweredAsync(question.Id, bob, cid);

        var reports = new List<HttpResponseMessage>();
        foreach (var session in new[] { ana, ana, bob })
            reports.Add(await PostAsync(session.Client, $"/api/questions/{question.Id}/report", new ReportQuestionRequest { Reason = "The key is wrong" }));
        var statusAfterTwo = await StatusAsync(question.Id);
        reports.Add(await PostAsync(cid.Client, $"/api/questions/{question.Id}/report", new ReportQuestionRequest { Reason = "Ambiguous" }));

        Assert.All(reports, r => Assert.Equal(HttpStatusCode.NoContent, r.StatusCode));
        Assert.Equal(QuestionStatus.Active, statusAfterTwo); // ana's second report does not count twice
        Assert.Equal(QuestionStatus.Quarantine, await StatusAsync(question.Id));
        var review = await ReviewAsync(ana, final.Id);
        Assert.True(review.Questions[3].Reported);
        Assert.False(review.Questions[2].Reported);
        Assert.Equal(final.Result!.Mine.Score, (await StateAsync(ana, final.Id)).Result!.Mine.Score);
    }

    private Task<QuestionStatus> StatusAsync(Guid questionId) =>
        App.InDbAsync(db => db.Questions.Where(q => q.Id == questionId).Select(q => q.Status).FirstAsync());

    // Only players who answered a question may report it; this gives other users an answer without playing.
    private Task AnsweredAsync(Guid questionId, params Session[] sessions) => App.InDbAsync(async db =>
    {
        var question = await db.Questions.FirstAsync(q => q.Id == questionId);
        foreach (var session in sessions)
            db.Answers.Add(new Answer
            {
                UserId = session.Auth.User.Id, QuestionId = question.Id, ConceptId = question.ConceptId, TopicId = question.TopicId,
                QuestionType = question.Type, Difficulty = question.Difficulty, Source = AnswerSource.Practice, IsCorrect = false,
                Fraction = 0, Points = 0, ElapsedMs = 1000, MasteryBefore = 50, MasteryAfter = 45, CreatedAt = App.Clock.GetUtcNow(),
            });
        return await db.SaveChangesAsync();
    });
}
