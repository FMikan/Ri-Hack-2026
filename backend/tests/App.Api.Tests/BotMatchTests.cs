using System.Net;
using App.Application.Matches;
using App.Domain;
using App.Domain.Content;
using App.Domain.Matches;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Tests;

public class BotMatchTests(TestApp app) : MatchTestBase(app)
{
    [Fact]
    public async Task Start_bot_match_returns_lobby_with_both_seats()
    {
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");

        var response = await PostAsync(ana.Client, "/api/matches/bot", new { persona = "senior", topicId = topic.Id });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var state = (await ReadAsync<MatchStateDto>(response))!;
        Assert.Equal(MatchStatus.Lobby, state.Status);
        Assert.Equal(MatchKind.Bot, state.Kind);
        Assert.Equal(0, state.MySeat);
        Assert.Equal(topic.Id, state.Topic!.Id);
        Assert.Equal("ana", state.Players[0].User!.Username);
        Assert.Equal(BotPersona.Senior, state.Players[1].Persona);
        Assert.Equal(1800, state.Players[1].DisplayRating);
        Assert.Null(state.Question);
        Assert.Equal(App.Clock.GetUtcNow().AddSeconds(5), state.PhaseDeadline);
    }

    [Fact]
    public async Task Start_bot_match_twice_is_409_already_in_match()
    {
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");
        await StartBotAsync(ana, topic.Id);

        var second = await PostAsync(ana.Client, "/api/matches/bot", new { persona = "mid", topicId = topic.Id });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(ErrorCodes.MatchAlreadyInMatch, (await ReadAsync<ProblemDetails>(second))!.Type);
    }

    [Fact]
    public async Task Unknown_topic_is_404()
    {
        var ana = await RegisterAsync("ana");

        var response = await PostAsync(ana.Client, "/api/matches/bot", new { persona = "mid", topicId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Open_question_hides_the_key_and_the_bot_answer()
    {
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");
        var started = await StartBotAsync(ana, topic.Id);

        await App.AdvanceAsync(TimeSpan.FromSeconds(5));
        var raw = await ana.Client.GetStringAsync($"/api/matches/{started.Id}");
        var state = await StateAsync(ana, started.Id);

        Assert.Equal(MatchPhase.Question, state.Phase);
        Assert.Equal(0, state.Question!.Index);
        Assert.Equal(10, state.Question.Total);
        Assert.Equal(QuestionType.TrueFalse, state.Question.Question.Type); // the warmup
        Assert.Equal("true_false", state.Question.Question.Content["type"]!.GetValue<string>());
        Assert.DoesNotContain("\"answer\"", raw);
        Assert.Null(state.Interstitial);
        Assert.NotNull(state.Question.DeliveredAt);
        Assert.Equal(0, state.Players[1].Score);
    }

    [Fact]
    public async Task Submit_answer_returns_my_answer_and_keeps_the_opponent_hidden()
    {
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");
        var state = await FirstQuestionAsync(ana, await StartBotAsync(ana, topic.Id));

        var response = await AnswerAsync(ana, state, Right(state));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var after = (await ReadAsync<MatchStateDto>(response))!;
        Assert.Equal(MatchPhase.Question, after.Phase); // the bot has not answered yet
        Assert.NotNull(after.Question!.MyAnswer);
        Assert.Null(after.Interstitial);
        Assert.False(after.Players[1].HasAnswered);
    }

    [Fact]
    public async Task Tick_after_bot_time_reveals_interstitial_with_both_results_and_key()
    {
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");
        var state = await FirstQuestionAsync(ana, await StartBotAsync(ana, topic.Id));
        var answered = (await ReadAsync<MatchStateDto>(await AnswerAsync(ana, state, Right(state))))!;
        Assert.True(answered.PhaseDeadline < state.PhaseDeadline); // now waits only for the bot, not the full time limit

        var after = await ToDeadlineAsync(ana, answered);

        Assert.Equal(MatchPhase.Interstitial, after.Phase);
        Assert.Equal(0, after.Interstitial!.Index);
        Assert.True(after.Interstitial.Mine.IsCorrect);
        Assert.NotNull(after.Interstitial.Opponent);
        Assert.Equal(Right(state).ToJsonString(), after.Interstitial.Key.Answer.ToJsonString());
        Assert.NotNull(after.Question); // a reload during the interstitial still has the question to render
        Assert.Equal(after.Interstitial.Mine.Points, after.Players[0].Score);
    }

    [Fact]
    public async Task Tick_after_interstitial_opens_the_next_question()
    {
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");
        var state = await FirstQuestionAsync(ana, await StartBotAsync(ana, topic.Id));
        var answered = (await ReadAsync<MatchStateDto>(await AnswerAsync(ana, state, Right(state))))!;
        var interstitial = await ToDeadlineAsync(ana, answered);
        Assert.Equal(interstitial.ServerTime.AddSeconds(GameRules.BotInterstitialSeconds), interstitial.PhaseDeadline);

        var next = await ToDeadlineAsync(ana, interstitial);

        Assert.Equal(MatchPhase.Question, next.Phase);
        Assert.Equal(1, next.Question!.Index);
        Assert.Null(next.Question.MyAnswer);
    }

    [Fact]
    public async Task Next_in_the_bot_interstitial_opens_the_next_question()
    {
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");
        var state = await FirstQuestionAsync(ana, await StartBotAsync(ana, topic.Id));
        var answered = (await ReadAsync<MatchStateDto>(await AnswerAsync(ana, state, Right(state))))!;
        var interstitial = await ToDeadlineAsync(ana, answered);
        Assert.Equal(MatchPhase.Interstitial, interstitial.Phase);

        var next = (await ReadAsync<MatchStateDto>(await ana.Client.PostAsync($"/api/matches/{state.Id}/next", null)))!;

        Assert.Equal(MatchPhase.Question, next.Phase);
        Assert.Equal(1, next.Question!.Index);
        Assert.Null(next.Question.MyAnswer);
    }

    [Fact]
    public async Task Answer_after_deadline_is_409_wrong_phase()
    {
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");
        var state = await FirstQuestionAsync(ana, await StartBotAsync(ana, topic.Id));
        App.Clock.Advance(TimeSpan.FromSeconds(30)); // the tick has not run yet

        var response = await AnswerAsync(ana, state, Right(state));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.MatchWrongPhase, (await ReadAsync<ProblemDetails>(response))!.Type);
    }

    [Fact]
    public async Task Second_answer_to_the_same_question_is_409()
    {
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");
        var state = await FirstQuestionAsync(ana, await StartBotAsync(ana, topic.Id));
        await AnswerAsync(ana, state, Right(state));

        var again = await AnswerAsync(ana, state, Right(state));

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(ErrorCodes.MatchAlreadyAnswered, (await ReadAsync<ProblemDetails>(again))!.Type);
    }

    [Fact]
    public async Task Non_participant_gets_403()
    {
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");
        var ivo = await RegisterAsync("ivo");
        var started = await StartBotAsync(ana, topic.Id);

        var response = await ivo.Client.GetAsync($"/api/matches/{started.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ErrorCodes.MatchNotParticipant, (await ReadAsync<ProblemDetails>(response))!.Type);
    }

    [Fact]
    public async Task Bots_list_recommends_the_persona_closest_to_my_rating()
    {
        var ana = await RegisterAsync("ana");

        var bots = (await ReadAsync<BotDto[]>(await ana.Client.GetAsync("/api/bots")))!;

        Assert.Equal(4, bots.Length);
        Assert.Equal(BotPersona.VibeCoder, Assert.Single(bots, b => b.Recommended).Persona);
    }
}
