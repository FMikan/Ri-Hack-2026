using System.Net.Http.Json;
using App.Application.Leaderboards;
using App.Application.Me;
using App.Domain;
using App.Domain.Matches;
using App.Domain.Progress;
using App.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace App.Api.Tests;

// F5a: the global, weekly, topic and friends boards with my own position, and the Grandmaster flag (idea §14).
public class LeaderboardTests(TestApp app) : ApiTestBase(app)
{
    private async Task<LeaderboardDto> BoardAsync(Session session, string board, string? topic = null) =>
        (await session.Client.GetFromJsonAsync<LeaderboardDto>($"/api/leaderboards/{board}{(topic is null ? "" : $"?topic={topic}")}", App.Json))!;

    private Task SetRatingAsync(Session session, int rating, int rankedMatches) =>
        App.InDbAsync(db => db.Users.Where(u => u.Id == session.Auth.User.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Rating, rating).SetProperty(u => u.RankedMatches, rankedMatches)));

    // Players who never log in during the test: straight into the table.
    private Task<List<Guid>> InsertPlayersAsync(int count, Func<int, int> rating, int rankedMatches = 10) => App.InDbAsync(async db =>
    {
        var users = Enumerable.Range(0, count).Select(i => new User
        {
            Email = $"p{i}@example.com", Username = $"p{i:000}", UsernameNormalized = $"p{i:000}", PasswordHash = "-",
            Rating = rating(i), RankedMatches = rankedMatches, CreatedAt = App.Clock.GetUtcNow(),
        }).ToList();
        db.Users.AddRange(users);
        await db.SaveChangesAsync();
        return users.Select(u => u.Id).ToList();
    });

    // A finished match as the history keeps it: only the seats' final scores matter to the weekly board.
    private Task FinishedMatchAsync(MatchKind kind, Session player, int score, MatchResultReason reason = MatchResultReason.Score) => App.InDbAsync(db =>
    {
        var now = App.Clock.GetUtcNow();
        var match = new Match { Kind = kind, RngSeed = 1, CreatedAt = now, Phase = MatchPhase.Done, FinishedAt = now, ResultReason = reason };
        match.Players.Add(new MatchPlayer { Seat = 0, Kind = SeatKind.Human, UserId = player.Auth.User.Id, DisplayRating = 1000, Score = score });
        match.Players.Add(new MatchPlayer { Seat = 1, Kind = kind == MatchKind.Ghost ? SeatKind.Ghost : SeatKind.Bot, DisplayRating = 1000, Score = 0 });
        db.Matches.Add(match);
        return db.SaveChangesAsync();
    });

    [Fact]
    public async Task Global_board_excludes_provisional_players()
    {
        var ana = await RegisterAsync("ana");
        var bob = await RegisterAsync("bob");
        var cid = await RegisterAsync("cid");
        await SetRatingAsync(ana, 1200, 10);
        await SetRatingAsync(bob, 1500, 9);
        await SetRatingAsync(cid, 1100, 30);

        var board = await BoardAsync(bob, "global");

        Assert.Equal(new[] { ("ana", 1200, 1), ("cid", 1100, 2) }, board.Entries.Select(e => (e.User.Username, e.Value, e.Rank)));
        Assert.Null(board.Me); // bob's rating is still provisional
        Assert.Null(board.ResetsAt);
        Assert.Equal(1, (await BoardAsync(ana, "global")).Me!.Entry.Rank);
    }

    [Fact]
    public async Task Top_100_global_are_grandmaster()
    {
        var ana = await RegisterAsync("ana");
        var bob = await RegisterAsync("bob");
        await SetRatingAsync(ana, 1200, 12);
        await SetRatingAsync(bob, 1900, 3);

        await App.HousekeepAsync();

        Assert.Equal("grandmaster", (await ana.Client.GetFromJsonAsync<MeDto>("/api/me", App.Json))!.Tier);
        Assert.Equal("bronze", (await bob.Client.GetFromJsonAsync<MeDto>("/api/me", App.Json))!.Tier); // provisional
        Assert.Equal("grandmaster", (await BoardAsync(ana, "global")).Entries[0].User.Tier);

        await InsertPlayersAsync(GameRules.GrandmasterTop, i => 1300 + i);
        await App.HousekeepAsync();

        Assert.Equal("bronze", (await ana.Client.GetFromJsonAsync<MeDto>("/api/me", App.Json))!.Tier); // 101st now
        Assert.Equal(GameRules.GrandmasterTop, await App.InDbAsync(db => db.Users.CountAsync(u => u.IsGrandmaster)));
    }

    [Fact]
    public async Task Weekly_board_resets_monday()
    {
        var ana = await RegisterAsync("ana");
        var bob = await RegisterAsync("bob");
        var cid = await RegisterAsync("cid");
        await FinishedMatchAsync(MatchKind.Ranked, ana, 500);
        await FinishedMatchAsync(MatchKind.Ranked, ana, 200);
        await FinishedMatchAsync(MatchKind.Ghost, bob, 300);
        await FinishedMatchAsync(MatchKind.Bot, cid, 900); // unranked: not on the board
        await FinishedMatchAsync(MatchKind.Ranked, cid, 999, MatchResultReason.Aborted);

        var board = await BoardAsync(ana, "weekly");

        Assert.Equal(new[] { ("ana", 700), ("bob", 300) }, board.Entries.Select(e => (e.User.Username, e.Value)));
        var monday = Clock.StartOf(Clock.WeekStart(App.Clock.GetUtcNow()).AddDays(7));
        Assert.Equal(monday, board.ResetsAt);

        App.Clock.Advance(TimeSpan.FromDays(7)); // always next week; whole days keep the shared clock's time of day
        Assert.Empty((await BoardAsync(await LoginAgainAsync(ana), "weekly")).Entries);
    }

    [Fact]
    public async Task Topic_board_counts_graduated()
    {
        var topic = (await SeedTopicsAsync(1))[0];
        var (a, b) = (topic.Concepts[0].Id, topic.Concepts[1].Id);
        var players = new Dictionary<string, Session>();
        foreach (var name in new[] { "ana", "bob", "cid", "dan", "eve" }) players[name] = await RegisterAsync(name);
        await App.InDbAsync(db =>
        {
            ConceptMastery Row(string user, Guid concept, double mastery, bool graduated) => new()
            {
                UserId = players[user].Auth.User.Id, ConceptId = concept, TopicId = topic.Id, Mastery = mastery, AnswerCount = 5, IsGraduated = graduated,
            };
            db.ConceptMastery.AddRange(
                Row("ana", a, 90, true), Row("ana", b, 88, true),
                Row("bob", a, 95, true), Row("bob", b, 60, false),
                Row("eve", a, 86, true), Row("eve", b, 50, false), // one graduated like bob, lower topic mastery
                Row("cid", a, 70, false));
            return db.SaveChangesAsync();
        });

        var board = await BoardAsync(players["dan"], "topic", topic.Slug);

        Assert.Equal(new[] { ("ana", 2), ("bob", 1), ("eve", 1), ("cid", 0) }, board.Entries.Select(e => (e.User.Username, e.Value)));
        Assert.Null(board.Me); // dan has not answered anything here
        Assert.Equal(System.Net.HttpStatusCode.NotFound, (await players["dan"].Client.GetAsync("/api/leaderboards/topic?topic=nope")).StatusCode);
    }

    [Fact]
    public async Task Friends_board_only_friends()
    {
        var ana = await RegisterAsync("ana");
        var bob = await RegisterAsync("bob");
        var cid = await RegisterAsync("cid");
        await RegisterAsync("dan");
        await SetRatingAsync(bob, 1300, 2);
        await SetRatingAsync(cid, 1400, 2);
        (await PostAsync(ana.Client, "/api/friends/requests", new { username = "bob" })).EnsureSuccessStatusCode();
        (await bob.Client.PostAsync($"/api/friends/requests/{ana.Auth.User.Id}/accept", null)).EnsureSuccessStatusCode();
        (await PostAsync(ana.Client, "/api/friends/requests", new { username = "cid" })).EnsureSuccessStatusCode(); // still pending

        var board = await BoardAsync(ana, "friends");

        Assert.Equal(new[] { ("bob", 1300), ("ana", 1000) }, board.Entries.Select(e => (e.User.Username, e.Value)));
        Assert.Equal(2, board.Me!.Entry.Rank);
    }

    [Fact]
    public async Task Board_includes_me_with_two_above_and_below()
    {
        var me = await RegisterAsync("mia");
        await InsertPlayersAsync(102, i => 1500 + 2 * i); // 1500, 1502, ... 1702

        await SetRatingAsync(me, 1551, 10);
        var middle = await BoardAsync(me, "global");

        Assert.Equal(GameRules.LeaderboardSize, middle.Entries.Length);
        Assert.Equal(77, middle.Me!.Entry.Rank); // 1552..1702 are above
        Assert.Equal(new[] { 75, 76, 77, 78, 79 }, middle.Me.Around.Select(e => e.Rank));
        Assert.Equal(new[] { 1554, 1552, 1551, 1550, 1548 }, middle.Me.Around.Select(e => e.Value));

        await SetRatingAsync(me, 1000, 10);
        var last = await BoardAsync(me, "global");

        Assert.DoesNotContain(last.Entries, e => e.User.Username == "mia");
        Assert.Equal(103, last.Me!.Entry.Rank);
        Assert.Equal(new[] { 101, 102, 103 }, last.Me.Around.Select(e => e.Rank));
    }
}
