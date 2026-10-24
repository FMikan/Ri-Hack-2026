using System.Net;
using System.Net.Http.Json;
using App.Application.Matches;
using App.Application.Me;
using App.Domain;
using App.Domain.Matches;
using App.Domain.Progress;
using App.Domain.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace App.Api.Tests;

// F3: a live ranked duel between two people: veto, play, Elo and tiers, resign and disconnects.
public class RankedMatchTests(TestApp app) : RankedTestBase(app)
{
    private async Task<(Session Ana, Session Bob, MatchStateDto Match)> RankedPairAsync(int anaRating = 1000, int bobRating = 1000)
    {
        var topics = await PlayableTopicsAsync(3);
        var ana = await PlayerAsync("ana", topics, anaRating);
        var bob = await PlayerAsync("bob", topics, bobRating);
        return (ana, bob, await RankedAsync(ana, bob));
    }

    private Task<User> UserAsync(Session session) => App.InDbAsync(db => db.Users.FirstAsync(u => u.Id == session.Auth.User.Id));

    [Fact]
    public async Task Veto_pool_shows_my_mastery_and_opponent_picks_only()
    {
        var t = await PlayableTopicsAsync(4);
        var ana = await PlayerAsync("ana", [t[0], t[1], t[2]]);
        var bob = await PlayerAsync("bob", [t[0], t[1], t[3]]);
        await App.InDbAsync(async db =>
        {
            var concept = await db.Concepts.FirstAsync(c => c.TopicId == t[0].Id);
            db.ConceptMastery.Add(new ConceptMastery { UserId = ana.Auth.User.Id, ConceptId = concept.Id, TopicId = t[0].Id, Mastery = 64, AnswerCount = 2 });
            return await db.SaveChangesAsync();
        });

        var match = await RankedAsync(ana, bob);
        var pool = match.Veto!.Pool.ToDictionary(p => p.Topic.Id);

        Assert.Equal(4, pool.Count);
        Assert.Equal(64, pool[t[0].Id].MyMastery);
        Assert.Null(pool[t[2].Id].MyMastery);
        Assert.Equal([true, true, false, true], t.Select(x => pool[x.Id].PickedByOpponent));
        Assert.Null((await StateAsync(bob, match.Id)).Veto!.Pool.Single(p => p.Topic.Id == t[0].Id).MyMastery); // never the opponent's
        Assert.Equal(2, match.Veto.BansRemaining);
    }

    [Fact]
    public async Task Ban_out_of_turn_is_409_and_ban_in_turn_passes_it()
    {
        var t = await PlayableTopicsAsync(4);
        var ana = await PlayerAsync("ana", [t[0], t[1], t[2]]);
        var bob = await PlayerAsync("bob", [t[0], t[1], t[3]]);
        var match = await ToDeadlineAsync(ana, await RankedAsync(ana, bob));
        var (onTurn, waiting) = match.Veto!.TurnSeat == 0 ? (ana, bob) : (bob, ana);

        var early = await BanAsync(waiting, match.Id, t[2].Id);
        var banned = (await ReadAsync<MatchStateDto>(await BanAsync(onTurn, match.Id, t[3].Id)))!;

        Assert.Equal(MatchPhase.VetoBan, match.Phase);
        Assert.Equal(ErrorCodes.MatchNotYourTurn, (await ReadAsync<ProblemDetails>(early))!.Type);
        Assert.Equal(1 - match.Veto.TurnSeat, banned.Veto!.TurnSeat);
        Assert.Equal(match.Veto.TurnSeat, banned.Veto.Pool.Single(p => p.Topic.Id == t[3].Id).BannedBySeat);
        Assert.Equal(1, banned.Veto.BansRemaining);
    }

    [Fact]
    public async Task Ban_timeout_auto_bans_and_passes_turn()
    {
        var (ana, bob, match) = await RankedPairAsync();
        var banning = await ToDeadlineAsync(ana, match);

        var after = await ToDeadlineAsync(ana, banning);

        Assert.Equal(MatchPhase.VetoPick, after.Phase); // a pool of three needs one ban
        var ban = after.Veto!.Pool.Single(p => p.BannedBySeat is not null);
        Assert.True(ban.Auto);
        Assert.Equal(banning.Veto!.TurnSeat, ban.BannedBySeat);
        Assert.NotNull(after.Veto.ChosenTopicId);
        Assert.Equal(2, after.Veto.FinalTwo!.Length);
    }

    [Fact]
    public async Task Veto_ends_with_random_topic_deck_and_lobby()
    {
        var (ana, bob, match) = await RankedPairAsync();
        var pick = await ToDeadlineAsync(ana, await ToDeadlineAsync(ana, match));

        var lobby = await ToDeadlineAsync(ana, pick);

        Assert.Equal(MatchPhase.Lobby, lobby.Phase);
        Assert.Equal(pick.Veto!.ChosenTopicId, lobby.Topic!.Id);
        Assert.Null(lobby.Veto);
        Assert.Equal(10, await App.InDbAsync(db => db.MatchQuestions.CountAsync(q => q.MatchId == match.Id)));
        Assert.Equal(pick.Veto.ChosenTopicId, await App.InDbAsync(db => db.MatchQuestions.Where(q => q.MatchId == match.Id)
            .Join(db.Questions, q => q.QuestionId, x => x.Id, (q, x) => (Guid?)x.TopicId).FirstAsync()));
    }

    [Fact]
    public async Task Live_match_full_playthrough_updates_elo_and_tier()
    {
        var (ana, bob, match) = await RankedPairAsync(anaRating: 1290, bobRating: 1290);
        var question = await ThroughVetoAsync(ana, bob, match);

        var (a, b) = await PlayBothAsync(ana, bob, question, right: (seat, _) => seat == 0);

        Assert.Equal(0, a.Result!.WinnerSeat);
        Assert.Equal((1290, 1306), (a.Result.Mine.RatingBefore, a.Result.Mine.RatingAfter));
        Assert.Equal((1290, 1274), (b.Result!.Mine.RatingBefore, b.Result.Mine.RatingAfter));
        var (anaUser, bobUser) = (await UserAsync(ana), await UserAsync(bob));
        Assert.Equal((1306, 1, Tier.Gold), (anaUser.Rating, anaUser.RankedMatches, anaUser.Tier));
        Assert.Equal((1274, 1, Tier.Silver), (bobUser.Rating, bobUser.RankedMatches, bobUser.Tier));
        Assert.Null(anaUser.CurrentMatchId);
        var history = await ana.Client.GetFromJsonAsync<MatchSummaryDto[]>("/api/me/matches", App.Json);
        Assert.Equal(16, Assert.Single(history!).RatingDelta);
        Assert.Equal(10, await App.InDbAsync(db => db.Answers.CountAsync(x => x.UserId == bob.Auth.User.Id && x.Source == AnswerSource.Ranked)));
    }

    [Fact]
    public async Task Simultaneous_answers_from_both_players_reach_interstitial()
    {
        var (ana, bob, match) = await RankedPairAsync();
        var question = await ThroughVetoAsync(ana, bob, match);
        var bobQuestion = await StateAsync(bob, match.Id);

        await Task.WhenAll(AnswerAsync(ana, question, Right(question)), AnswerAsync(bob, bobQuestion, Right(bobQuestion)));

        var after = await StateAsync(ana, match.Id);
        Assert.Equal(MatchPhase.Interstitial, after.Phase);
        Assert.NotNull(after.Interstitial!.Opponent);
    }

    [Fact]
    public async Task Provisional_rating_hidden_until_10_matches()
    {
        var (ana, bob, match) = await RankedPairAsync();
        await App.InDbAsync(db => db.Users.Where(u => u.Id == ana.Auth.User.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.RankedMatches, 9)));
        var question = await ThroughVetoAsync(ana, bob, match);

        await PlayBothAsync(ana, bob, question, right: (_, _) => true);

        Assert.False((await ana.Client.GetFromJsonAsync<MeDto>("/api/me", App.Json))!.IsProvisional);
        Assert.True((await bob.Client.GetFromJsonAsync<MeDto>("/api/me", App.Json))!.IsProvisional);
    }

    [Fact]
    public async Task Resign_gives_full_loss()
    {
        var (ana, bob, match) = await RankedPairAsync();
        await ThroughVetoAsync(ana, bob, match);

        var resigned = (await ReadAsync<MatchStateDto>(await bob.Client.PostAsync($"/api/matches/{match.Id}/resign", null)))!;

        Assert.Equal(MatchResultReason.Resign, resigned.Result!.Reason);
        Assert.Equal(0, resigned.Result.WinnerSeat);
        Assert.Equal(984, resigned.Result.Mine.RatingAfter);
        Assert.Equal(1016, (await UserAsync(ana)).Rating);
    }

    [Fact]
    public async Task Disconnect_20s_marks_disconnected_questions_timeout_without_mastery_and_end_counts_as_resign()
    {
        var (ana, bob, match) = await RankedPairAsync();
        var state = await ThroughVetoAsync(ana, bob, match);

        // Only ana's client keeps polling and answering; bob is gone after his last poll.
        for (var step = 0; step < 100 && state.Status != MatchStatus.Finished; step++)
        {
            state = await StateAsync(ana, match.Id);
            if (state.Phase == MatchPhase.Question && state.Question!.MyAnswer is null)
                state = (await ReadAsync<MatchStateDto>(await AnswerAsync(ana, state, Right(state))))!;
            else if (state.Status != MatchStatus.Finished)
                state = await ToDeadlineAsync(ana, state);
        }

        Assert.Equal(MatchResultReason.Disconnect, state.Result!.Reason);
        Assert.Equal(0, state.Result.WinnerSeat);
        Assert.Equal(984, (await UserAsync(bob)).Rating);
        // The first timeouts fell inside bob's 20 s grace and count; once he was gone they no longer teach.
        var bobAnswers = await App.InDbAsync(db => db.Answers.CountAsync(a => a.UserId == bob.Auth.User.Id));
        Assert.InRange(bobAnswers, 1, 9);
        Assert.False((await StateAsync(ana, match.Id)).Players[1].Connected);
    }

    [Fact]
    public async Task Both_disconnected_aborts_without_rating_changes()
    {
        var (ana, bob, match) = await RankedPairAsync();
        await ThroughVetoAsync(ana, bob, match);

        for (var i = 0; i < 40; i++) await App.AdvanceAsync(TimeSpan.FromSeconds(10)); // nobody polls any more

        var final = await StateAsync(await LoginAgainAsync(ana), match.Id);
        Assert.Equal(MatchStatus.Aborted, final.Status);
        Assert.Null(final.Result!.WinnerSeat);
        Assert.Equal((1000, 0), ((await UserAsync(ana)).Rating, (await UserAsync(ana)).RankedMatches));
        Assert.Equal(1000, (await UserAsync(bob)).Rating);
    }

    [Fact]
    public async Task Identical_answers_are_a_draw_that_keeps_equal_ratings()
    {
        var (ana, bob, match) = await RankedPairAsync();
        var question = await ThroughVetoAsync(ana, bob, match);

        var (a, _) = await PlayBothAsync(ana, bob, question, right: (_, index) => index % 2 == 0);

        Assert.Equal(MatchResultReason.Draw, a.Result!.Reason);
        Assert.Null(a.Result.WinnerSeat);
        Assert.Equal((1000, 1), ((await UserAsync(ana)).Rating, (await UserAsync(ana)).RankedMatches));
    }
}
