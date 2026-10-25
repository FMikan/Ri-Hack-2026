using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using App.Application.Matches;
using App.Application.Me;
using App.Application.Social;
using App.Domain;
using App.Domain.Content;
using App.Domain.Matches;
using App.Domain.Progress;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace App.Api.Tests;

// F5a: friends, friend challenges, the public profile and statistics (idea §2, §12).
public class SocialTests(TestApp app) : RankedTestBase(app)
{
    private async Task<string?> ErrorAsync(HttpResponseMessage response) => (await ReadAsync<ProblemDetails>(response))!.Type;

    private Task<HttpResponseMessage> RequestFriendAsync(Session from, Session to) =>
        PostAsync(from.Client, "/api/friends/requests", new FriendRequest { Username = to.Auth.User.Username });

    private async Task<FriendDto[]> FriendsAsync(Session session) =>
        (await session.Client.GetFromJsonAsync<FriendDto[]>("/api/friends", App.Json))!;

    private async Task FriendsWithAsync(Session a, Session b)
    {
        (await RequestFriendAsync(a, b)).EnsureSuccessStatusCode();
        (await b.Client.PostAsync($"/api/friends/requests/{a.Auth.User.Id}/accept", null)).EnsureSuccessStatusCode();
    }

    private async Task<FriendChallengeCreatedDto> ChallengeAsync(Session host, Guid? friend = null)
    {
        var response = await PostAsync(host.Client, "/api/friend-challenges", new CreateFriendChallengeRequest { FriendUserId = friend });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadAsync<FriendChallengeCreatedDto>(response))!;
    }

    [Fact]
    public async Task Friend_request_accept_remove()
    {
        var ana = await RegisterAsync("ana");
        var bob = await RegisterAsync("bob");

        var sent = await RequestFriendAsync(ana, bob);

        Assert.Equal(HttpStatusCode.Created, sent.StatusCode);
        Assert.Equal(FriendStatus.PendingOut, (await ReadAsync<FriendDto>(sent))!.Status);
        var incoming = Assert.Single(await FriendsAsync(bob));
        Assert.Equal(("ana", FriendStatus.PendingIn), (incoming.User.Username, incoming.Status));

        var accepted = await bob.Client.PostAsync($"/api/friends/requests/{ana.Auth.User.Id}/accept", null);

        Assert.Equal(FriendStatus.Accepted, (await ReadAsync<FriendDto>(accepted))!.Status);
        Assert.Equal(FriendStatus.Accepted, Assert.Single(await FriendsAsync(ana)).Status);

        Assert.Equal(HttpStatusCode.NoContent, (await ana.Client.DeleteAsync($"/api/friends/{bob.Auth.User.Id}")).StatusCode);
        Assert.Empty(await FriendsAsync(ana));
        Assert.Empty(await FriendsAsync(bob));
        Assert.Equal(HttpStatusCode.NotFound, (await bob.Client.PostAsync($"/api/friends/requests/{ana.Auth.User.Id}/accept", null)).StatusCode);
    }

    [Fact]
    public async Task Friend_request_in_both_directions_is_one_row()
    {
        var ana = await RegisterAsync("ana");
        var bob = await RegisterAsync("bob");

        (await RequestFriendAsync(ana, bob)).EnsureSuccessStatusCode();
        var back = await RequestFriendAsync(bob, ana);

        Assert.Equal(FriendStatus.Accepted, (await ReadAsync<FriendDto>(back))!.Status); // asking back accepts
        Assert.Equal(1, await App.InDbAsync(db => db.Friendships.CountAsync()));
        Assert.Equal(ErrorCodes.FriendsAlready, await ErrorAsync(await RequestFriendAsync(ana, bob)));
        Assert.Equal(ErrorCodes.FriendsSelf, await ErrorAsync(await RequestFriendAsync(ana, ana)));
        var unknown = await PostAsync(ana.Client, "/api/friends/requests", new FriendRequest { Username = "nobody" });
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task Online_status_from_last_seen()
    {
        var ana = await RegisterAsync("ana");
        var bob = await RegisterAsync("bob");
        await FriendsWithAsync(ana, bob); // bob's accept is a request: bob is online

        Assert.True(Assert.Single(await FriendsAsync(ana)).Online);

        App.Clock.Advance(TimeSpan.FromMinutes(3));
        Assert.False(Assert.Single(await FriendsAsync(ana)).Online);

        await bob.Client.GetAsync("/api/me");
        Assert.True(Assert.Single(await FriendsAsync(ana)).Online);
    }

    [Fact]
    public async Task Friend_challenge_code_joins_veto_unranked_5_coins_each()
    {
        var topics = await PlayableTopicsAsync(3);
        var ana = await PlayerAsync("ana", topics);
        var bob = await PlayerAsync("bob", topics);
        var challenge = await ChallengeAsync(ana);
        Assert.Matches("^[A-Z2-9]{6}$", challenge.Code);
        Assert.Equal(App.Clock.GetUtcNow().AddMinutes(10), challenge.ExpiresAt);

        var open = (await bob.Client.GetFromJsonAsync<FriendChallengeDto>($"/api/friend-challenges/{challenge.Code.ToLowerInvariant()}", App.Json))!;
        Assert.Equal(("ana", null), (open.Host.Username, open.MatchId));
        Assert.Equal(ErrorCodes.ChallengeOwn, await ErrorAsync(await ana.Client.PostAsync($"/api/friend-challenges/{challenge.Code}/join", null)));

        var joined = await bob.Client.PostAsync($"/api/friend-challenges/{challenge.Code}/join", null);

        Assert.Equal(HttpStatusCode.Created, joined.StatusCode);
        var match = (await ReadAsync<MatchStateDto>(joined))!;
        Assert.Equal((MatchKind.Friend, MatchPhase.VetoCoin, 1), (match.Kind, match.Phase, match.MySeat));
        var hostView = (await ana.Client.GetFromJsonAsync<FriendChallengeDto>($"/api/friend-challenges/{challenge.Code}", App.Json))!;
        Assert.Equal(match.Id, hostView.MatchId);
        Assert.Equal(ErrorCodes.ChallengeExpired, await ErrorAsync(await (await RegisterAsync("cid")).Client.PostAsync($"/api/friend-challenges/{challenge.Code}/join", null)));

        var (a, b) = await PlayBothAsync(ana, bob, await ThroughVetoAsync(ana, bob, await StateAsync(ana, match.Id)), right: (seat, _) => seat == 0);

        Assert.Equal(0, a.Result!.WinnerSeat);
        Assert.Equal((5, 0), (a.Result.Mine.Coins, a.Result.Mine.Sp));
        Assert.Equal((5, 0), (b.Result!.Mine.Coins, b.Result.Mine.Sp));
        Assert.Null(a.Result.Mine.RatingAfter);
        Assert.True(a.Result.RematchAvailable);
        var me = (await ana.Client.GetFromJsonAsync<MeDto>("/api/me", App.Json))!;
        Assert.Equal((1000, 0, 5), (me.Rating, me.RankedMatches, me.Coins));
    }

    [Fact]
    public async Task Targeted_friend_challenge_appears_in_inbox()
    {
        var topics = await PlayableTopicsAsync(3);
        var ana = await PlayerAsync("ana", topics);
        var bob = await PlayerAsync("bob", topics);
        var cid = await PlayerAsync("cid", topics);
        await FriendsWithAsync(ana, bob);

        var notFriend = await PostAsync(ana.Client, "/api/friend-challenges", new CreateFriendChallengeRequest { FriendUserId = cid.Auth.User.Id });
        Assert.Equal(ErrorCodes.FriendsNotFriends, await ErrorAsync(notFriend));

        var challenge = await ChallengeAsync(ana, bob.Auth.User.Id);
        Assert.Equal(App.Clock.GetUtcNow().AddSeconds(GameRules.FriendInviteSeconds), challenge.ExpiresAt);

        var inbox = (await bob.Client.GetFromJsonAsync<DashboardDto>("/api/me/dashboard", App.Json))!.Inbox.FriendChallenge!;
        Assert.Equal((challenge.Code, "ana", challenge.ExpiresAt), (inbox.Code, inbox.Host.Username, inbox.ExpiresAt));
        Assert.Equal(challenge.Code, (await bob.Client.GetFromJsonAsync<InboxDto>("/api/me/inbox", App.Json))!.FriendChallenge!.Code);
        Assert.Null((await cid.Client.GetFromJsonAsync<DashboardDto>("/api/me/dashboard", App.Json))!.Inbox.FriendChallenge);

        App.Clock.Advance(TimeSpan.FromSeconds(GameRules.FriendInviteSeconds + 1));
        Assert.Null((await bob.Client.GetFromJsonAsync<DashboardDto>("/api/me/dashboard", App.Json))!.Inbox.FriendChallenge);
        Assert.Null((await bob.Client.GetFromJsonAsync<InboxDto>("/api/me/inbox", App.Json))!.FriendChallenge);
        Assert.Equal(ErrorCodes.ChallengeExpired, await ErrorAsync(await bob.Client.PostAsync($"/api/friend-challenges/{challenge.Code}/join", null)));
    }

    [Fact]
    public async Task Friend_declines_targeted_challenge()
    {
        var topics = await PlayableTopicsAsync(3);
        var ana = await PlayerAsync("ana", topics);
        var bob = await PlayerAsync("bob", topics);
        var cid = await PlayerAsync("cid", topics);
        await FriendsWithAsync(ana, bob);
        var challenge = await ChallengeAsync(ana, bob.Auth.User.Id);

        // Only the friend it was sent to can decline it; anyone else changes nothing.
        Assert.Equal(HttpStatusCode.NoContent, (await cid.Client.PostAsync($"/api/friend-challenges/{challenge.Code}/decline", null)).StatusCode);
        Assert.NotNull((await bob.Client.GetFromJsonAsync<InboxDto>("/api/me/inbox", App.Json))!.FriendChallenge);

        Assert.Equal(HttpStatusCode.NoContent, (await bob.Client.PostAsync($"/api/friend-challenges/{challenge.Code}/decline", null)).StatusCode);

        Assert.Null((await bob.Client.GetFromJsonAsync<InboxDto>("/api/me/inbox", App.Json))!.FriendChallenge);
        var hostView = (await ana.Client.GetFromJsonAsync<FriendChallengeDto>($"/api/friend-challenges/{challenge.Code}", App.Json))!;
        Assert.True(hostView.ExpiresAt <= App.Clock.GetUtcNow());
        Assert.Equal(ErrorCodes.ChallengeExpired, await ErrorAsync(await bob.Client.PostAsync($"/api/friend-challenges/{challenge.Code}/join", null)));
    }

    [Fact]
    public async Task Public_profile_hides_concept_mastery()
    {
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");
        await FinishedBotMatchAsync(ana, topic.Id);
        var bob = await RegisterAsync("bob");

        var response = await bob.Client.GetAsync("/api/users/ANA");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.DoesNotContain("\"concepts\"", json.ToJsonString()); // topic level only: no per-concept mastery
        var profile = (await ReadAsync<PublicProfileDto>(response))!;
        Assert.Equal("ana", profile.User.Username);
        Assert.Equal(1, profile.Stats.Matches);
        Assert.NotNull(Assert.Single(profile.Topics, t => t.Topic.Id == topic.Id).Mastery);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.Client.GetAsync("/api/users/nobody")).StatusCode);
    }

    [Fact]
    public async Task Friends_only_profile_is_403_for_strangers()
    {
        var ana = await RegisterAsync("ana");
        var bob = await RegisterAsync("bob");
        (await ana.Client.PatchAsJsonAsync("/api/me", new { profileVisibility = "friends" }, App.Json)).EnsureSuccessStatusCode();

        var stranger = await bob.Client.GetAsync("/api/users/ana");

        Assert.Equal(HttpStatusCode.Forbidden, stranger.StatusCode);
        Assert.Equal(ErrorCodes.ProfilePrivate, await ErrorAsync(stranger));
        Assert.Equal(HttpStatusCode.OK, (await ana.Client.GetAsync("/api/users/ana")).StatusCode);
        await FriendsWithAsync(ana, bob);
        Assert.Equal(HttpStatusCode.OK, (await bob.Client.GetAsync("/api/users/ana")).StatusCode);
    }

    [Fact]
    public async Task Stats_accuracy_by_type_and_most_common_blunder()
    {
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");
        await App.InDbAsync(async db =>
        {
            var questions = await db.Questions.Where(q => q.TopicId == topic.Id && q.Difficulty == 1).ToListAsync();
            Answer Row(QuestionType type, bool correct, AnswerLabel label, int elapsedMs)
            {
                var q = questions.First(q => q.Type == type);
                return new Answer
                {
                    UserId = ana.Auth.User.Id, QuestionId = q.Id, ConceptId = q.ConceptId, TopicId = topic.Id, QuestionType = type, Difficulty = 1,
                    Source = AnswerSource.Practice, IsCorrect = correct, Fraction = correct ? 1 : 0, Points = 0, ElapsedMs = elapsedMs,
                    TimeLimitMs = 15_000, Label = label, CreatedAt = App.Clock.GetUtcNow(),
                };
            }
            db.Answers.AddRange(
                Row(QuestionType.MultipleChoice, true, AnswerLabel.Good, 4_000),
                Row(QuestionType.MultipleChoice, false, AnswerLabel.Blunder, 2_000),
                Row(QuestionType.FindBug, false, AnswerLabel.Blunder, 1_000),
                Row(QuestionType.FindBug, false, AnswerLabel.Blunder, 3_000),
                Row(QuestionType.FindBug, true, AnswerLabel.Brilliant, 5_000));
            return await db.SaveChangesAsync();
        });

        var stats = (await ana.Client.GetFromJsonAsync<StatsDto>("/api/me/stats", App.Json))!;

        Assert.Equal(50, stats.AccuracyByType["multiple_choice"]);
        Assert.Equal(33.3, stats.AccuracyByType["find_bug"]);
        Assert.False(stats.AccuracyByType.ContainsKey("true_false")); // never answered
        Assert.Equal(3_000, stats.AvgTimeMsByType["find_bug"]);
        Assert.Equal(QuestionType.FindBug, stats.MostCommonBlunderType);
        Assert.Equal(1, stats.BrilliantCount);
        Assert.Equal((0, 0.0), (stats.Matches, stats.WinRate));
    }

    [Fact]
    public async Task Stats_and_dashboard_show_results_and_rating_history()
    {
        var topics = await PlayableTopicsAsync(3);
        var ana = await PlayerAsync("ana", topics);
        var bob = await PlayerAsync("bob", topics);
        var match = await RankedAsync(ana, bob);
        var (a, _) = await PlayBothAsync(ana, bob, await ThroughVetoAsync(ana, bob, match), right: (seat, _) => seat == 0);

        var stats = (await ana.Client.GetFromJsonAsync<StatsDto>("/api/me/stats", App.Json))!;
        var bobStats = (await bob.Client.GetFromJsonAsync<StatsDto>("/api/me/stats", App.Json))!;
        var dashboard = (await ana.Client.GetFromJsonAsync<DashboardDto>("/api/me/dashboard", App.Json))!;

        Assert.Equal((1, 1, 0, 0, 100.0), (stats.Matches, stats.Wins, stats.Losses, stats.Draws, stats.WinRate));
        Assert.Equal((1, 0, 1, 0.0), (bobStats.Matches, bobStats.Wins, bobStats.Losses, bobStats.WinRate));
        Assert.Equal(a.Result!.Mine.RatingAfter, Assert.Single(stats.RatingHistory).Rating);
        Assert.Equal(stats.RatingHistory, dashboard.RatingHistory);
    }
}
