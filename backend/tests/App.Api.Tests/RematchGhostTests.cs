using System.Net;
using System.Net.Http.Json;
using App.Application.Matches;
using App.Application.Me;
using App.Application.Queue;
using App.Domain;
using App.Domain.Content;
using App.Domain.Matches;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace App.Api.Tests;

// F3b: the rematch after a ranked match and the ghost duel against a recording (idea §4, §10).
public class RematchGhostTests(TestApp app) : RankedTestBase(app)
{
    private async Task<(Session Ana, Session Bob, MatchStateDto Final)> FinishedRankedAsync(Topic[] topics, string a = "ana", string b = "bob")
    {
        var ana = await PlayerAsync(a, topics);
        var bob = await PlayerAsync(b, topics);
        var match = await RankedAsync(ana, bob);
        var (final, _) = await PlayBothAsync(ana, bob, await ThroughVetoAsync(ana, bob, match), right: (seat, index) => seat == 0 || index < 3);
        return (ana, bob, final);
    }

    private async Task<RematchDto> RematchAsync(Session session, Guid matchId)
    {
        var response = await session.Client.PostAsync($"/api/matches/{matchId}/rematch", null);
        response.EnsureSuccessStatusCode();
        return (await ReadAsync<RematchDto>(response))!;
    }

    private Task<RematchDto> RematchStateAsync(Session session, Guid matchId) =>
        session.Client.GetFromJsonAsync<RematchDto>($"/api/matches/{matchId}/rematch", App.Json)!;

    [Fact]
    public async Task Rematch_accept_skips_queue_and_starts_veto()
    {
        var (ana, bob, final) = await FinishedRankedAsync(await PlayableTopicsAsync(3));
        Assert.True(final.Result!.RematchAvailable);

        var requested = await RematchAsync(ana, final.Id);
        var inbox = (await bob.Client.GetFromJsonAsync<DashboardDto>("/api/me/dashboard", App.Json))!.Inbox.Rematch;
        var accepted = await bob.Client.PostAsync($"/api/matches/{final.Id}/rematch/accept", null);
        var rematch = (await ReadAsync<MatchStateDto>(accepted))!;

        Assert.Equal(RematchStatus.Pending, requested.Status);
        Assert.Equal(0, requested.RequestedBySeat);
        Assert.Equal(App.Clock.GetUtcNow().AddSeconds(20), requested.ExpiresAt);
        Assert.Equal(final.Id, inbox!.MatchId);
        Assert.Equal("ana", inbox.From.Username);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal((MatchKind.Ranked, MatchPhase.VetoCoin), (rematch.Kind, rematch.Phase));
        Assert.Equal(new RematchDto(RematchStatus.Accepted, 0, requested.ExpiresAt, rematch.Id), await RematchStateAsync(ana, final.Id));
        Assert.Equal(rematch.Id, (await ana.Client.GetFromJsonAsync<MeDto>("/api/me", App.Json))!.ActiveMatchId);
        Assert.Equal(final.Id, await App.InDbAsync(db => db.Matches.Where(m => m.Id == rematch.Id).Select(m => m.RematchOfMatchId).FirstAsync()));
    }

    [Fact]
    public async Task Rematch_request_expires_in_20s()
    {
        var (ana, bob, final) = await FinishedRankedAsync(await PlayableTopicsAsync(3));
        await RematchAsync(ana, final.Id);

        App.Clock.Advance(TimeSpan.FromSeconds(20));
        var late = await bob.Client.PostAsync($"/api/matches/{final.Id}/rematch/accept", null);

        Assert.Equal(RematchStatus.Expired, (await RematchStateAsync(bob, final.Id)).Status);
        Assert.Equal(ErrorCodes.MatchRematchUnavailable, (await ReadAsync<ProblemDetails>(late))!.Type);
        Assert.Null((await bob.Client.GetFromJsonAsync<DashboardDto>("/api/me/dashboard", App.Json))!.Inbox.Rematch);
    }

    [Fact]
    public async Task Both_asking_for_a_rematch_starts_it()
    {
        var (ana, bob, final) = await FinishedRankedAsync(await PlayableTopicsAsync(3));

        await RematchAsync(ana, final.Id);
        var both = await RematchAsync(bob, final.Id);

        Assert.Equal(RematchStatus.Accepted, both.Status);
        Assert.Equal(MatchKind.Ranked, (await StateAsync(bob, both.MatchId!.Value)).Kind);
    }

    [Fact]
    public async Task Rematch_is_unavailable_once_the_opponent_has_left()
    {
        var (ana, _, final) = await FinishedRankedAsync(await PlayableTopicsAsync(3));

        App.Clock.Advance(TimeSpan.FromMinutes(3)); // only ana keeps the result screen open
        var state = await StateAsync(ana, final.Id);
        var response = await ana.Client.PostAsync($"/api/matches/{final.Id}/rematch", null);

        Assert.False(state.Result!.RematchAvailable);
        Assert.Equal(ErrorCodes.MatchRematchUnavailable, (await ReadAsync<ProblemDetails>(response))!.Type);
    }

    // A finished ranked match between two other players: the recording a waiting player can be offered.
    private async Task<(Topic[] Topics, MatchStateDto Recording)> RecordingAsync()
    {
        var topics = await PlayableTopicsAsync(3);
        var (_, _, final) = await FinishedRankedAsync(topics, "cid", "dan");
        return (topics, final);
    }

    private async Task<QueueStateDto> WaitedAsync(Session session)
    {
        (await JoinAsync(session)).EnsureSuccessStatusCode();
        await App.MatchmakeAsync();
        App.Clock.Advance(TimeSpan.FromSeconds(30));
        await App.MatchmakeAsync();
        return await QueueAsync(session);
    }

    [Fact]
    public async Task Ghost_offer_after_30s_from_recent_recording_in_range()
    {
        var (topics, recording) = await RecordingAsync();
        var ana = await PlayerAsync("ana", topics);
        (await JoinAsync(ana)).EnsureSuccessStatusCode();
        await App.MatchmakeAsync();
        var early = await QueueAsync(ana);

        App.Clock.Advance(TimeSpan.FromSeconds(30));
        await App.MatchmakeAsync();
        var offered = await QueueAsync(ana);

        Assert.Null(early.GhostOffer);
        Assert.Contains(offered.GhostOffer!.Opponent.Username, new[] { "cid", "dan" });
        Assert.Equal(recording.Topic!.Id, offered.GhostOffer.Topic.Id);
        Assert.False(offered.BotOffer);
    }

    [Fact]
    public async Task Ghost_match_replays_recorded_answers_and_changes_only_live_player_rating()
    {
        var (topics, recording) = await RecordingAsync();
        var ana = await PlayerAsync("ana", topics);
        var offer = (await WaitedAsync(ana)).GhostOffer!;
        var recordedRating = await App.InDbAsync(db => db.Users.Where(u => u.Username == offer.Opponent.Username).Select(u => u.Rating).FirstAsync());

        var response = await ana.Client.PostAsync("/api/queue/ghost", null);
        var started = (await ReadAsync<MatchStateDto>(response))!;
        var first = await FirstQuestionAsync(ana, started);
        var answered = (await ReadAsync<MatchStateDto>(await AnswerAsync(ana, first, Right(first))))!;
        var final = await PlayToEndAsync(ana, answered, right: index => index != 5); // never a draw with either recording

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal((MatchKind.Ghost, MatchPhase.Lobby), (started.Kind, started.Phase));
        Assert.Equal(recording.Topic!.Id, started.Topic!.Id);
        Assert.Equal(SeatKind.Ghost, started.Players[1].Kind);
        Assert.Equal(offer.Opponent.Username, started.Players[1].User!.Username);
        Assert.Equal(offer.RecordedAt, started.Players[1].GhostRecordedAt);
        Assert.Equal(MatchPhase.Question, answered.Phase); // the ghost answers at its recorded time, not at once
        var source = await App.InDbAsync(db => db.MatchQuestions.Where(q => q.MatchId == recording.Id).OrderBy(q => q.Index).Select(q => q.QuestionId).ToListAsync());
        Assert.Equal(source, await App.InDbAsync(db => db.MatchQuestions.Where(q => q.MatchId == started.Id).OrderBy(q => q.Index).Select(q => q.QuestionId).ToListAsync()));
        Assert.Equal(1000, final.Result!.Mine.RatingBefore);
        Assert.NotEqual(1000, final.Result.Mine.RatingAfter);
        Assert.Equal(recordedRating, await App.InDbAsync(db => db.Users.Where(u => u.Username == offer.Opponent.Username).Select(u => u.Rating).FirstAsync()));
        Assert.False(final.Result.RematchAvailable);
        Assert.Equal(QueuePhase.Idle, (await QueueAsync(ana)).Status);
    }

    [Fact]
    public async Task Declined_ghost_offers_bot_and_the_same_recording_is_offered_once()
    {
        var (topics, _) = await RecordingAsync();
        var ana = await PlayerAsync("ana", topics);
        await WaitedAsync(ana);

        var declined = (await ReadAsync<QueueStateDto>(await ana.Client.PostAsync("/api/queue/ghost/decline", null)))!;
        await ana.Client.DeleteAsync("/api/queue");
        var again = await WaitedAsync(ana);
        var none = await ana.Client.PostAsync("/api/queue/ghost", null);

        Assert.Null(declined.GhostOffer);
        Assert.True(declined.BotOffer);
        Assert.Null(again.GhostOffer); // both seats of that match are one recording, already offered
        Assert.True(again.BotOffer);
        Assert.Equal(ErrorCodes.QueueNoGhostOffer, (await ReadAsync<ProblemDetails>(none))!.Type);
    }

    [Fact]
    public async Task Recordings_older_than_30_days_are_not_offered()
    {
        var (topics, recording) = await RecordingAsync();
        await App.InDbAsync(db => db.Matches.Where(m => m.Id == recording.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.FinishedAt, App.Clock.GetUtcNow().AddDays(-31))));
        var ana = await PlayerAsync("ana", topics);

        var waited = await WaitedAsync(ana);

        Assert.Null(waited.GhostOffer);
        Assert.True(waited.BotOffer);
    }
}
