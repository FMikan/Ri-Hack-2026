using System.Net;
using System.Net.Http.Json;
using App.Application;
using App.Application.Content;
using App.Application.Economy;
using App.Application.Leaderboards;
using App.Application.Lessons;
using App.Application.Matches;
using App.Application.Me;
using App.Domain;
using App.Domain.Content;
using App.Domain.Economy;
using App.Domain.Matches;
using App.Domain.Progress;
using App.Domain.Seasons;
using App.Domain.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace App.Api.Tests;

// F5b: seasons, titles, achievements, the weekly snapshot, retiring and recalibrating questions (idea §13, §14, §17).
public class SeasonTests(TestApp app) : MatchTestBase(app)
{
    private async Task GrantSpAsync(Session session, int sp)
    {
        using var scope = App.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<Rewarder>()
            .GrantAsync(session.Auth.User.Id, new Grant(RewardType.MatchWin, 0, 0, sp, null, $"sp:{Guid.NewGuid()}"), default);
    }

    private Task<Season> SeasonAsync(int number) => App.InDbAsync(db => db.Seasons.AsNoTracking().FirstAsync(s => s.Number == number));

    private async Task<TitleDto[]> TitlesAsync(Session session) => (await session.Client.GetFromJsonAsync<TitleDto[]>("/api/me/titles", App.Json))!;

    private Task FinishedRankedAsync(Session player, int score) => App.InDbAsync(db =>
    {
        var now = App.Clock.GetUtcNow();
        var match = new Match { Kind = MatchKind.Ranked, RngSeed = 1, CreatedAt = now, Phase = MatchPhase.Done, FinishedAt = now, ResultReason = MatchResultReason.Score, WinnerSeat = 0 };
        match.Players.Add(new MatchPlayer { Seat = 0, Kind = SeatKind.Human, UserId = player.Auth.User.Id, DisplayRating = 1000, Score = score });
        match.Players.Add(new MatchPlayer { Seat = 1, Kind = SeatKind.Bot, DisplayRating = 1000 });
        db.Matches.Add(match);
        return db.SaveChangesAsync();
    });

    [Fact]
    public async Task Season_board_orders_by_sp()
    {
        var ana = await RegisterAsync("ana");
        var bob = await RegisterAsync("bob");
        await RegisterAsync("cid");
        await GrantSpAsync(ana, 30);
        await GrantSpAsync(bob, 20);
        await GrantSpAsync(bob, 30);

        var board = (await ana.Client.GetFromJsonAsync<LeaderboardDto>("/api/leaderboards/season", App.Json))!;
        var season = (await ana.Client.GetFromJsonAsync<SeasonDto>("/api/seasons/current", App.Json))!;

        Assert.Equal(new[] { ("bob", 50), ("ana", 30) }, board.Entries.Select(e => (e.User.Username, e.Value)));
        Assert.Equal((1, false), (season.Number, season.DoubleLearningSp));
        Assert.Equal(season.EndsAt, board.ResetsAt);
        Assert.Equal(30, (await ana.Client.GetFromJsonAsync<MeDto>("/api/me", App.Json))!.SeasonPoints);
        Assert.Equal(50, await App.InDbAsync(db => db.RewardEvents.Where(e => e.UserId == bob.Auth.User.Id && e.SeasonId == season.Id).SumAsync(e => e.Sp)));
    }

    [Fact]
    public async Task Patched_sp_doubles_in_first_two_weeks_of_season()
    {
        await App.InDbAsync(db => db.Seasons.ExecuteUpdateAsync(s => s.SetProperty(x => x.StartsAt, App.Clock.GetUtcNow().AddDays(-13))));
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");
        var match = await FinishedBotMatchAsync(ana, topic.Id);
        var lesson = (await ReadAsync<LessonDto>(await PostAsync(ana.Client, "/api/lessons", new StartLessonRequest { MatchId = match.Id })))!;

        var result = (await ReadAsync<LessonResultDto>(await PostAsync(ana.Client, $"/api/lessons/{lesson.Id}/check", new
        {
            answers = lesson.Check.Select(c => new { questionId = c.Question.Id, payload = QuestionFixtures.RightAnswer(c.Question.Type) }),
        })))!;

        Assert.True((await ana.Client.GetFromJsonAsync<SeasonDto>("/api/seasons/current", App.Json))!.DoubleLearningSp);
        Assert.True(result.Passed);
        Assert.Equal(Math.Min(4 * lesson.Concepts.Length, 20), result.Rewards.Sp); // 2 × 2 SP per concept, 2 × the daily cap of 10
    }

    [Fact]
    public async Task Close_season_snapshots_ranks_awards_titles_and_tier_cosmetic_resets_sp_keeps_elo()
    {
        var ana = await RegisterAsync("ana");
        var bob = await RegisterAsync("bob");
        var cid = await RegisterAsync("cid");
        var season = await SeasonAsync(1);
        await App.InDbAsync(async db =>
        {
            await db.Users.Where(u => u.Id == ana.Auth.User.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.Rating, 1750).SetProperty(u => u.Tier, Tier.Diamond));
            db.UserSeasons.Add(new UserSeason { UserId = cid.Auth.User.Id, SeasonId = season.Id, BestTier = Tier.Gold }); // played, no SP
            db.ShopItems.Add(new ShopItem { Key = "frame.season-diamond", Kind = ShopItemKind.AvatarFrame, Name = "D", Price = 1000, AssetKey = "frame-season-diamond", SeasonDropTier = "diamond", IsActive = false });
            return await db.SaveChangesAsync();
        });
        await GrantSpAsync(ana, 30);
        await GrantSpAsync(bob, 10);

        App.Clock.Advance(TimeSpan.FromDays(61));
        await App.HousekeepAsync();
        await App.HousekeepAsync(); // closing twice changes nothing
        (ana, bob, cid) = (await LoginAgainAsync(ana), await LoginAgainAsync(bob), await LoginAgainAsync(cid));

        var ranks = await App.InDbAsync(db => db.UserSeasons.Where(s => s.SeasonId == season.Id).ToDictionaryAsync(s => s.UserId, s => s.FinalRank));
        Assert.Equal((1, 2, null), (ranks[ana.Auth.User.Id], ranks[bob.Auth.User.Id], ranks[cid.Auth.User.Id]));
        Assert.Equal(new[] { "s1.diamond", "s1.top5" }, (await TitlesAsync(ana)).Select(t => t.Key).Order());
        Assert.Equal(new[] { "s1.bronze", "s1.top5" }, (await TitlesAsync(bob)).Select(t => t.Key).Order());
        Assert.Equal("S1 Gold", Assert.Single(await TitlesAsync(cid)).Name);
        Assert.All(await TitlesAsync(ana), t => Assert.Equal((1, TitleKind.Season), (t.Count, t.Kind)));
        Assert.True(await App.InDbAsync(db => db.UserItems.AnyAsync(i => i.UserId == ana.Auth.User.Id)));
        Assert.False(await App.InDbAsync(db => db.UserItems.AnyAsync(i => i.UserId == bob.Auth.User.Id)));

        var me = (await ana.Client.GetFromJsonAsync<MeDto>("/api/me", App.Json))!;
        Assert.Equal((0, 1750), (me.SeasonPoints, me.Rating));
        var current = (await ana.Client.GetFromJsonAsync<SeasonDto>("/api/seasons/current", App.Json))!;
        Assert.Equal((2, season.EndsAt), (current.Number, current.StartsAt));
        Assert.NotNull((await SeasonAsync(1)).ClosedAt);
    }

    [Fact]
    public async Task Close_week_awards_top10_badge_count()
    {
        var ana = await RegisterAsync("ana");
        var bob = await RegisterAsync("bob");
        await FinishedRankedAsync(ana, 500);
        await FinishedRankedAsync(bob, 0);

        App.Clock.Advance(TimeSpan.FromDays(7));
        await App.HousekeepAsync();
        await App.HousekeepAsync();
        await FinishedRankedAsync(ana, 100);
        App.Clock.Advance(TimeSpan.FromDays(7));
        await App.HousekeepAsync();
        ana = await LoginAgainAsync(ana);

        var weekly = Assert.Single(await TitlesAsync(ana));
        Assert.Equal(("weekly.top10", 2, TitleKind.Weekly), (weekly.Key, weekly.Count, weekly.Kind));
        Assert.Empty(await TitlesAsync(await LoginAgainAsync(bob))); // on the board with 0 points, no badge
        var results = await App.InDbAsync(db => db.WeeklyResults.OrderBy(r => r.WeekStart).ThenBy(r => r.Rank).ToListAsync());
        Assert.Equal(new[] { (1, 500), (2, 0), (1, 100) }, results.Select(r => (r.Rank, r.Points)));
    }

    [Fact]
    public async Task Knowledge_title_for_100_percent_graduated_and_lost_below_70()
    {
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");
        await App.InDbAsync(db =>
        {
            db.ConceptMastery.AddRange(topic.Concepts.Select(c => new ConceptMastery
            {
                UserId = ana.Auth.User.Id, ConceptId = c.Id, TopicId = topic.Id, Mastery = 71, AnswerCount = 10, IsGraduated = true,
            }));
            return db.SaveChangesAsync();
        });
        await App.HousekeepAsync();

        var title = Assert.Single(await TitlesAsync(ana));
        Assert.Equal(($"master.{topic.Slug}", TitleKind.Knowledge, true), (title.Key, title.Kind, title.Active));
        var notEarned = await ana.Client.PutAsJsonAsync("/api/me/equipment", new { titleId = Guid.NewGuid() }, App.Json);
        Assert.Equal(ErrorCodes.ShopNotOwned, (await ReadAsync<ProblemDetails>(notEarned))!.Type);
        var equipped = await ReadAsync<MeDto>(await ana.Client.PutAsJsonAsync("/api/me/equipment", new { titleId = title.Id }, App.Json));
        Assert.Equal((title.Name, title.Id), (equipped!.Title, equipped.EquippedTitleId));
        Assert.Equal(title.Name, (await ana.Client.GetFromJsonAsync<LeaderboardDto>("/api/leaderboards/friends", App.Json))!.Entries[0].User.Title);

        // Fast wrong answers drop graduated concepts below 70: they lose graduation, and the title goes with them.
        var practice = await ReadAsync<MatchStateDto>(await PostAsync(ana.Client, "/api/matches/practice", new { topicId = topic.Id, timed = true }));
        await PlayToEndAsync(ana, await FirstQuestionAsync(ana, practice!), right: _ => false);
        await App.HousekeepAsync();

        Assert.False(Assert.Single(await TitlesAsync(ana)).Active);
        var me = (await ana.Client.GetFromJsonAsync<MeDto>("/api/me", App.Json))!;
        Assert.Equal((null, null), (me.Title, me.EquippedTitleId));
    }

    [Fact]
    public async Task Retiring_question_reverts_its_mastery_deltas_and_keeps_match_result()
    {
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");
        var match = await FinishedBotMatchAsync(ana, topic.Id, right: _ => true);
        var question = await App.InDbAsync(db => db.MatchQuestions.FirstAsync(q => q.MatchId == match.Id && q.Index == 0));
        var mastery = () => App.InDbAsync(db => db.ConceptMastery.Where(m => m.UserId == ana.Auth.User.Id && m.ConceptId == question.ConceptId).Select(m => m.Mastery).FirstAsync());
        Assert.True(await mastery() > GameRules.MasteryStart);

        await App.InDbAsync(async db =>
        {
            await ((IAppDbContext)db).RetireQuestionAsync(question.QuestionId, App.Clock.GetUtcNow(), default);
            return 0;
        });

        Assert.Equal(GameRules.MasteryStart, await mastery(), precision: 6); // the deck never repeats a concept: only this answer moved it
        Assert.Equal(QuestionStatus.Retired, await App.InDbAsync(db => db.Questions.Where(q => q.Id == question.QuestionId).Select(q => q.Status).FirstAsync()));
        Assert.NotNull(await App.InDbAsync(db => db.Answers.Where(a => a.QuestionId == question.QuestionId).Select(a => a.RevertedAt).FirstAsync()));
        var after = await StateAsync(ana, match.Id);
        Assert.Equal((match.Result!.Mine.Score, match.Result.Mine.Correct), (after.Result!.Mine.Score, after.Result.Mine.Correct));
        Assert.Equal(9, await App.InDbAsync(db => db.Answers.CountAsync(a => a.UserId == ana.Auth.User.Id && a.RevertedAt == null))); // what statistics still count
    }

    [Fact]
    public async Task Question_moves_down_a_band_when_too_easy()
    {
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");
        var (easy, hard) = await App.InDbAsync(async db =>
            (await db.Questions.FirstAsync(q => q.TopicId == topic.Id && q.Difficulty == 2), await db.Questions.FirstAsync(q => q.TopicId == topic.Id && q.Difficulty == 1)));
        await App.InDbAsync(db =>
        {
            Answer Row(Question q, bool correct) => new()
            {
                UserId = ana.Auth.User.Id, QuestionId = q.Id, ConceptId = q.ConceptId, TopicId = topic.Id, QuestionType = q.Type, Difficulty = q.Difficulty,
                Source = AnswerSource.Practice, IsCorrect = correct, Fraction = correct ? 1 : 0, Points = 0, ElapsedMs = 1_000, CreatedAt = App.Clock.GetUtcNow(),
            };
            db.Answers.AddRange(Enumerable.Range(0, 50).Select(i => Row(easy, i >= 4))); // 92 % right
            db.Answers.AddRange(Enumerable.Range(0, 50).Select(i => Row(hard, i < 10))); // 20 % right
            return db.SaveChangesAsync();
        });

        await DailyAsync();
        await DailyAsync(); // the answers were given at the old difficulty: no second move

        Assert.Equal(1, await DifficultyAsync(easy.Id));
        Assert.Equal(2, await DifficultyAsync(hard.Id));
    }

    [Fact]
    public async Task Achievements_record_first_win_and_show_progress()
    {
        var ana = await RegisterAsync("ana");
        var bob = await RegisterAsync("bob");
        await FinishedRankedAsync(ana, 300);

        var achievements = (await ana.Client.GetFromJsonAsync<AchievementDto[]>("/api/me/achievements", App.Json))!;
        App.Clock.Advance(TimeSpan.FromMinutes(1));
        var again = (await ana.Client.GetFromJsonAsync<AchievementDto[]>("/api/me/achievements", App.Json))!;

        var first = Assert.Single(achievements, a => a.Key == "first_win");
        Assert.NotNull(first.EarnedAt);
        Assert.Equal(first.EarnedAt, Assert.Single(again, a => a.Key == "first_win").EarnedAt); // recorded once
        var brilliant = Assert.Single(achievements, a => a.Key == "brilliant_10");
        Assert.Equal((null, 0, 10), (brilliant.EarnedAt, brilliant.Progress, brilliant.Target));
        Assert.Equal(new[] { "first_win" }, (await bob.Client.GetFromJsonAsync<PublicProfileDto>("/api/users/ana", App.Json))!.Achievements);
    }

    private async Task DailyAsync()
    {
        using var scope = App.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<App.Application.Seasons.HousekeepingService>().DailyAsync(default);
    }

    private Task<int> DifficultyAsync(Guid questionId) => App.InDbAsync(db => db.Questions.Where(q => q.Id == questionId).Select(q => q.Difficulty).FirstAsync());
}
