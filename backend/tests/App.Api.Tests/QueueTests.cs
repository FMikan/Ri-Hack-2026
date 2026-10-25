using System.Net;
using System.Net.Http.Json;
using App.Application.Matches;
using App.Application.Me;
using App.Application.Queue;
using App.Domain;
using App.Domain.Matches;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace App.Api.Tests;

// F3: the ranked queue and "opponent found" (idea §4). Topics here need no questions: nobody plays.
public class QueueTests(TestApp app) : RankedTestBase(app)
{
    [Fact]
    public async Task Join_queue_requires_topics()
    {
        var ana = await RegisterAsync("ana");

        var response = await JoinAsync(ana);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.TopicsCount, (await ReadAsync<ProblemDetails>(response))!.Type);
    }

    [Fact]
    public async Task Join_twice_is_409()
    {
        var ana = await PlayerAsync("ana", await SeedTopicsAsync(1, 2, 3));
        (await JoinAsync(ana)).EnsureSuccessStatusCode();

        var again = await JoinAsync(ana);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(ErrorCodes.QueueAlreadyIn, (await ReadAsync<ProblemDetails>(again))!.Type);
    }

    [Fact]
    public async Task Join_while_in_match_is_409()
    {
        var topic = await SeedTopicAsync();
        var ana = await PlayerAsync("ana", [.. await SeedTopicsAsync(1, 2), topic]);
        await StartBotAsync(ana, topic.Id);

        var response = await JoinAsync(ana);

        Assert.Equal(ErrorCodes.MatchAlreadyInMatch, (await ReadAsync<ProblemDetails>(response))!.Type);
    }

    [Fact]
    public async Task Set_topics_is_locked_while_in_queue()
    {
        var topics = await SeedTopicsAsync(1, 2, 3, 4);
        var ana = await PlayerAsync("ana", topics[..3]);
        (await JoinAsync(ana)).EnsureSuccessStatusCode();

        var locked = await ana.Client.PutAsJsonAsync("/api/me/topics", new { topicIds = topics[1..].Select(t => t.Id) }, App.Json);
        var me = await ana.Client.GetFromJsonAsync<MeDto>("/api/me", App.Json);
        var left = await ana.Client.DeleteAsync("/api/queue");
        var unlocked = await ana.Client.PutAsJsonAsync("/api/me/topics", new { topicIds = topics[1..].Select(t => t.Id) }, App.Json);

        Assert.Equal(ErrorCodes.TopicsLockedInQueue, (await ReadAsync<ProblemDetails>(locked))!.Type);
        Assert.True(me!.InQueue);
        Assert.Equal(HttpStatusCode.NoContent, left.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, unlocked.StatusCode);
        Assert.Equal(QueuePhase.Idle, (await QueueAsync(ana)).Status);
    }

    [Fact]
    public async Task Two_compatible_players_get_proposal_within_one_tick()
    {
        var topics = await SeedTopicsAsync(1, 2, 3, 4);
        var ana = await PlayerAsync("ana", topics[..3]);
        var bob = await PlayerAsync("bob", topics[1..]);
        await JoinAsync(ana);
        await JoinAsync(bob);
        var waiting = await QueueAsync(ana);

        await App.MatchmakeAsync();
        var (a, b) = (await QueueAsync(ana), await QueueAsync(bob));

        Assert.Equal(QueuePhase.Waiting, waiting.Status);
        Assert.Equal(100, waiting.RangeNow);
        Assert.Equal(topics[..3].Select(t => t.Id).Order(), waiting.TopicIds.Order());
        Assert.Equal((QueuePhase.Proposed, QueuePhase.Proposed), (a.Status, b.Status));
        Assert.Equal(a.Proposal!.Id, b.Proposal!.Id);
        Assert.Equal("bob", a.Proposal.Opponent.Username);
        Assert.Equal("ana", b.Proposal.Opponent.Username);
        Assert.Equal(App.Clock.GetUtcNow().AddSeconds(10), a.Proposal.ExpiresAt);
        Assert.False(a.Proposal.IAccepted);
    }

    [Fact]
    public async Task Incompatible_topics_never_match()
    {
        var topics = await SeedTopicsAsync(1, 2, 3, 4, 5, 6);
        var ana = await PlayerAsync("ana", topics[..3]);
        var bob = await PlayerAsync("bob", topics[3..]);
        await JoinAsync(ana);
        await JoinAsync(bob);

        App.Clock.Advance(TimeSpan.FromMinutes(5));
        await App.MatchmakeAsync();

        Assert.Equal(QueuePhase.Waiting, (await QueueAsync(await LoginAgainAsync(ana))).Status);
    }

    [Fact]
    public async Task Range_widens_with_time_and_matches_after_20s()
    {
        var topics = await SeedTopicsAsync(1, 2, 3);
        var ana = await PlayerAsync("ana", topics, rating: 1000);
        var bob = await PlayerAsync("bob", topics, rating: 1200);
        await JoinAsync(ana);
        await JoinAsync(bob);

        await App.MatchmakeAsync();
        var early = await QueueAsync(ana);
        App.Clock.Advance(TimeSpan.FromSeconds(20));
        await App.MatchmakeAsync();

        Assert.Equal(QueuePhase.Waiting, early.Status);
        Assert.Equal(QueuePhase.Proposed, (await QueueAsync(ana)).Status);
    }

    [Fact]
    public async Task Proposal_expires_after_10s_decliner_leaves_queue_other_keeps_wait_time()
    {
        var topics = await SeedTopicsAsync(1, 2, 3);
        var ana = await PlayerAsync("ana", topics);
        var bob = await PlayerAsync("bob", topics);
        await JoinAsync(ana);
        var enteredAt = (await QueueAsync(ana)).EnteredAt;
        App.Clock.Advance(TimeSpan.FromSeconds(3));
        await JoinAsync(bob);
        await App.MatchmakeAsync();
        await AcceptAsync(ana, (await QueueAsync(ana)).Proposal!.Id);

        App.Clock.Advance(TimeSpan.FromSeconds(10));
        await App.MatchmakeAsync();
        var (a, b) = (await QueueAsync(ana), await QueueAsync(bob));

        Assert.Equal(QueuePhase.Waiting, a.Status);
        Assert.Equal(enteredAt, a.EnteredAt);
        Assert.Null(a.Proposal);
        Assert.Equal(QueuePhase.Idle, b.Status);
    }

    [Fact]
    public async Task Leaving_queue_while_proposed_declines_and_returns_other_to_waiting()
    {
        var topics = await SeedTopicsAsync(1, 2, 3);
        var ana = await PlayerAsync("ana", topics);
        var bob = await PlayerAsync("bob", topics);
        await JoinAsync(ana);
        await JoinAsync(bob);
        await App.MatchmakeAsync();
        var proposal = (await QueueAsync(ana)).Proposal!;

        var left = await bob.Client.DeleteAsync("/api/queue");
        var late = await ana.Client.PostAsync($"/api/queue/proposals/{proposal.Id}/accept", null);

        Assert.Equal(HttpStatusCode.NoContent, left.StatusCode);
        Assert.Equal(QueuePhase.Waiting, (await QueueAsync(ana)).Status);
        Assert.Equal(QueuePhase.Idle, (await QueueAsync(bob)).Status);
        Assert.Equal(ErrorCodes.ProposalExpired, (await ReadAsync<ProblemDetails>(late))!.Type);
    }

    [Fact]
    public async Task Both_accept_creates_ranked_match_in_veto_coin()
    {
        var topics = await PlayableTopicsAsync(3);
        var ana = await PlayerAsync("ana", topics);
        var bob = await PlayerAsync("bob", topics);
        await JoinAsync(ana);
        await JoinAsync(bob);
        await App.MatchmakeAsync();
        var proposal = (await QueueAsync(ana)).Proposal!;

        var first = await AcceptAsync(ana, proposal.Id);
        var second = await AcceptAsync(bob, proposal.Id);
        var match = await StateAsync(ana, second.MatchId!.Value);

        Assert.Equal(QueuePhase.Proposed, first.Status);
        Assert.True(first.Proposal!.IAccepted);
        Assert.Equal(QueuePhase.Matched, second.Status);
        Assert.Equal(second.MatchId, (await QueueAsync(ana)).MatchId);
        Assert.Equal(MatchKind.Ranked, match.Kind);
        Assert.Equal(MatchPhase.VetoCoin, match.Phase);
        Assert.Equal(MatchStatus.Veto, match.Status);
        Assert.Equal(3, match.Veto!.Pool.Length);
        Assert.Equal(1, match.Veto.BansRemaining);
        Assert.Null(match.Topic);
        Assert.Equal(match.Id, (await bob.Client.GetFromJsonAsync<MeDto>("/api/me", App.Json))!.ActiveMatchId);
    }

    [Fact]
    public async Task Simultaneous_accepts_create_exactly_one_match()
    {
        var topics = await PlayableTopicsAsync(3);
        var ana = await PlayerAsync("ana", topics);
        var bob = await PlayerAsync("bob", topics);
        await JoinAsync(ana);
        await JoinAsync(bob);
        await App.MatchmakeAsync();
        var proposal = (await QueueAsync(ana)).Proposal!;

        await Task.WhenAll(AcceptAsync(ana, proposal.Id), AcceptAsync(bob, proposal.Id));

        var matches = await App.InDbAsync(db => db.Matches.Where(m => m.Kind == MatchKind.Ranked).Select(m => m.Id).ToListAsync());
        var match = Assert.Single(matches);
        Assert.Equal(match, (await QueueAsync(ana)).MatchId);
        Assert.Equal(match, (await QueueAsync(bob)).MatchId);
    }

    [Fact]
    public async Task Starting_a_bot_match_leaves_the_queue()
    {
        var topic = await SeedTopicAsync();
        var ana = await PlayerAsync("ana", [.. await SeedTopicsAsync(1, 2), topic]);
        await JoinAsync(ana);

        await StartBotAsync(ana, topic.Id);

        Assert.Equal(QueuePhase.Idle, (await QueueAsync(ana)).Status);
    }

    [Fact]
    public async Task Bot_is_offered_after_20s_without_an_opponent()
    {
        var ana = await PlayerAsync("ana", await SeedTopicsAsync(1, 2, 3));
        await JoinAsync(ana);
        var early = await QueueAsync(ana);

        App.Clock.Advance(TimeSpan.FromSeconds(20));

        Assert.False(early.BotOffer);
        Assert.True((await QueueAsync(ana)).BotOffer);
    }
}
