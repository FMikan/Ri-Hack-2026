using System.Net.Http.Json;
using App.Application.Matches;
using App.Application.Queue;
using App.Domain.Content;
using App.Domain.Matches;
using Microsoft.EntityFrameworkCore;

namespace App.Api.Tests;

// Two players, the ranked queue and the veto, as their clients would drive them.
public abstract class RankedTestBase(TestApp app) : MatchTestBase(app)
{
    // t1..tN, each a full question pool, so whichever topic the veto draws can build a deck.
    protected async Task<Topic[]> PlayableTopicsAsync(int count)
    {
        var topics = new List<Topic>();
        for (var i = 1; i <= count; i++)
            topics.Add(await App.InDbAsync(db => QuestionFixtures.SeedPlayableTopicAsync(db, slug: $"t{i}", now: App.Clock.GetUtcNow())));
        return [.. topics];
    }

    protected async Task<Session> PlayerAsync(string name, IEnumerable<Topic> topics, int? rating = null)
    {
        var session = await RegisterAsync(name);
        (await session.Client.PutAsJsonAsync("/api/me/topics", new { topicIds = topics.Select(t => t.Id) }, App.Json)).EnsureSuccessStatusCode();
        if (rating is { } r)
            await App.InDbAsync(db => db.Users.Where(u => u.Id == session.Auth.User.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.Rating, r)));
        return session;
    }

    protected Task<HttpResponseMessage> JoinAsync(Session session) => session.Client.PostAsync("/api/queue", null);

    protected async Task<QueueStateDto> QueueAsync(Session session) =>
        (await session.Client.GetFromJsonAsync<QueueStateDto>("/api/queue", App.Json))!;

    protected async Task<QueueStateDto> AcceptAsync(Session session, Guid proposalId)
    {
        var response = await session.Client.PostAsync($"/api/queue/proposals/{proposalId}/accept", null);
        response.EnsureSuccessStatusCode();
        return (await ReadAsync<QueueStateDto>(response))!;
    }

    // Both join, the matchmaker pairs them, both accept: the ranked match in its first veto phase.
    protected async Task<MatchStateDto> RankedAsync(Session a, Session b)
    {
        (await JoinAsync(a)).EnsureSuccessStatusCode();
        (await JoinAsync(b)).EnsureSuccessStatusCode();
        await App.MatchmakeAsync();
        var proposal = (await QueueAsync(a)).Proposal!;
        await AcceptAsync(a, proposal.Id);
        var matched = await AcceptAsync(b, proposal.Id);
        return await StateAsync(a, matched.MatchId!.Value);
    }

    protected Task<HttpResponseMessage> BanAsync(Session session, Guid matchId, Guid topicId) =>
        PostAsync(session.Client, $"/api/matches/{matchId}/veto/ban", new BanTopicRequest { TopicId = topicId });

    // Lets every veto deadline pass (auto-bans) until the first question is open for both players.
    protected async Task<MatchStateDto> ThroughVetoAsync(Session a, Session b, MatchStateDto state)
    {
        for (var step = 0; step < 20 && state.Phase != MatchPhase.Question; step++)
        {
            state = await ToDeadlineAsync(a, state);
            await StateAsync(b, state.Id); // b's client polls too, so b stays connected
        }
        Assert.Equal(MatchPhase.Question, state.Phase);
        return state;
    }

    // Both players answer every question (right where `right(seat, index)` says) until the match ends.
    protected async Task<(MatchStateDto A, MatchStateDto B)> PlayBothAsync(Session a, Session b, MatchStateDto state, Func<int, int, bool> right)
    {
        Session[] players = [a, b];
        for (var step = 0; step < 100 && state.Status != MatchStatus.Finished; step++)
        {
            var answered = false;
            foreach (var (session, seat) in players.Select((s, i) => (s, i)))
            {
                var mine = await StateAsync(session, state.Id);
                if (mine.Phase != MatchPhase.Question || mine.Question!.MyAnswer is not null) continue;
                var payload = right(seat, mine.Question.Index) ? Right(mine) : QuestionFixtures.WrongAnswer;
                state = (await ReadAsync<MatchStateDto>(await AnswerAsync(session, mine, payload)))!;
                answered = true;
            }
            if (!answered) state = await ToDeadlineAsync(a, await StateAsync(a, state.Id));
        }
        Assert.Equal(MatchStatus.Finished, state.Status);
        return (await StateAsync(a, state.Id), await StateAsync(b, state.Id));
    }
}
