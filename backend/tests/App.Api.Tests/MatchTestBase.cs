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
    protected async Task<MatchStateDto> ToDeadlineAsync(Session session, MatchStateDto state)
    {
        App.Clock.SetUtcNow(state.PhaseDeadline!.Value);
        await App.TickAsync();
        return await StateAsync(session, state.Id);
    }

    // From the lobby to the first open question, as the player's client would see it.
    protected async Task<MatchStateDto> FirstQuestionAsync(Session session, MatchStateDto started)
    {
        await App.AdvanceAsync(TimeSpan.FromSeconds(5));
        return await StateAsync(session, started.Id);
    }
}
