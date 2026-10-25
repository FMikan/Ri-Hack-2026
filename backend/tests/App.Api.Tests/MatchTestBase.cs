using System.Net.Http.Json;
using System.Text.Json.Nodes;
using App.Application.Matches;
using App.Domain.Content;
using App.Domain.Matches;

namespace App.Api.Tests;

// Shared steps for match tests: a playable topic, starting matches, answering, and moving the fake clock.
public abstract class MatchTestBase(TestApp app) : ApiTestBase(app)
{
    protected Task<Topic> SeedTopicAsync() => App.InDbAsync(db => QuestionFixtures.SeedPlayableTopicAsync(db, now: App.Clock.GetUtcNow()));

    protected async Task<MatchStateDto> StartBotAsync(Session session, Guid topicId, BotPersona persona = BotPersona.Senior)
    {
        var response = await PostAsync(session.Client, "/api/matches/bot", new { persona, topicId });
        response.EnsureSuccessStatusCode();
        return (await ReadAsync<MatchStateDto>(response))!;
    }

    protected async Task<MatchStateDto> StateAsync(Session session, Guid matchId) =>
        (await session.Client.GetFromJsonAsync<MatchStateDto>($"/api/matches/{matchId}", App.Json))!;

    protected Task<HttpResponseMessage> AnswerAsync(Session session, MatchStateDto state, JsonNode payload, int elapsedMs = 500) =>
        PostAsync(session.Client, $"/api/matches/{state.Id}/answers", new { questionIndex = state.Question!.Index, payload, clientElapsedMs = elapsedMs });

    protected static JsonNode Right(MatchStateDto state) => QuestionFixtures.RightAnswer(state.Question!.Question.Type);

    // Moves the clock exactly to the phase deadline the client was given, runs the tick, and returns the new state.
    // A waiting client polls once a second, so one poll right before the tick keeps it connected through a 30 s bot interstitial.
    protected async Task<MatchStateDto> ToDeadlineAsync(Session session, MatchStateDto state)
    {
        App.Clock.SetUtcNow(state.PhaseDeadline!.Value);
        await StateAsync(session, state.Id);
        await App.TickAsync();
        return await StateAsync(session, state.Id);
    }

    // From the lobby to the first open question, as the player's client would see it.
    protected async Task<MatchStateDto> FirstQuestionAsync(Session session, MatchStateDto started)
    {
        await App.AdvanceAsync(TimeSpan.FromSeconds(5));
        return await StateAsync(session, started.Id);
    }

    // Answers every open question (right where `right(index)` says so, by default on even indexes) and follows the
    // deadlines to the end of the match; in a bot duel it taps "next" instead of sitting out the 30 s interstitial.
    protected async Task<MatchStateDto> PlayToEndAsync(Session session, MatchStateDto state, Func<int, bool>? right = null)
    {
        right ??= index => index % 2 == 0;
        for (var step = 0; step < 100 && state.Status != MatchStatus.Finished; step++)
        {
            if (state.Phase == MatchPhase.Question && state.Question!.MyAnswer is null)
            {
                var payload = right(state.Question.Index) ? Right(state) : QuestionFixtures.WrongAnswer;
                state = (await ReadAsync<MatchStateDto>(await AnswerAsync(session, state, payload)))!;
                continue;
            }
            if (state.Phase == MatchPhase.Interstitial && state.Kind == MatchKind.Bot)
            {
                state = (await ReadAsync<MatchStateDto>(await session.Client.PostAsync($"/api/matches/{state.Id}/next", null)))!;
                continue;
            }
            state = await ToDeadlineAsync(session, state);
        }
        Assert.Equal(MatchStatus.Finished, state.Status);
        return state;
    }

    // A finished bot match: right on even questions, wrong on odd ones, unless `right` says otherwise.
    protected async Task<MatchStateDto> FinishedBotMatchAsync(Session session, Guid topicId, Func<int, bool>? right = null) =>
        await PlayToEndAsync(session, await FirstQuestionAsync(session, await StartBotAsync(session, topicId)), right);
}
