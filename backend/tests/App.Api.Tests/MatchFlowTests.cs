using System.Net;
using System.Net.Http.Json;
using App.Application.Matches;
using App.Application.Me;
using App.Domain;
using App.Domain.Matches;
using App.Domain.Progress;
using Microsoft.EntityFrameworkCore;

namespace App.Api.Tests;

public class MatchFlowTests(TestApp app) : MatchTestBase(app)
{
    [Fact]
    public async Task Full_bot_match_finishes_with_result_cards_and_no_rating_change()
    {
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");

        var final = await PlayToEndAsync(ana, await FirstQuestionAsync(ana, await StartBotAsync(ana, topic.Id)));

        var result = final.Result!;
        Assert.Equal(5, result.Mine.Correct);
        Assert.Equal(final.Players[0].Score, result.Mine.Score);
        Assert.NotNull(result.Opponent);
        Assert.Contains(result.Reason, new[] { MatchResultReason.Score, MatchResultReason.TiebreakCorrect, MatchResultReason.TiebreakTime, MatchResultReason.Draw });
        Assert.Null(result.Mine.RatingBefore);
        Assert.NotNull(result.Cards.Blunder);
        Assert.Equal(1, result.Cards.Blunder!.Index % 2); // only odd questions were answered wrong
        Assert.NotNull(result.Cards.Brilliant);
        Assert.NotNull(result.Cards.PracticeConcept);
        Assert.True(result.Cards.PracticeConcept!.MasteryDelta < 0);
        Assert.Null(final.Question);
        Assert.Null(final.PhaseDeadline);

        var me = (await ana.Client.GetFromJsonAsync<MeDto>("/api/me", App.Json))!;
        Assert.Equal(1000, me.Rating);
        Assert.Null(me.ActiveMatchId);
    }

    [Fact]
    public async Task Finishing_a_match_frees_the_player_for_the_next_one()
    {
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");
        var first = await StartBotAsync(ana, topic.Id);
        Assert.Equal(first.Id, (await ana.Client.GetFromJsonAsync<MeDto>("/api/me", App.Json))!.ActiveMatchId);

        await PlayToEndAsync(ana, await FirstQuestionAsync(ana, first));
        var second = await PostAsync(ana.Client, "/api/matches/bot", new { persona = "mid", topicId = topic.Id });

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Fact]
    public async Task Each_answer_writes_answers_row_and_updates_concept_mastery()
    {
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");

        var final = await PlayToEndAsync(ana, await FirstQuestionAsync(ana, await StartBotAsync(ana, topic.Id)));

        var (answers, mastery) = await App.InDbAsync(async db => (
            await db.Answers.Where(a => a.UserId == ana.Auth.User.Id).ToListAsync(),
            await db.ConceptMastery.Where(m => m.UserId == ana.Auth.User.Id).ToListAsync()));
        Assert.Equal(10, answers.Count);
        Assert.All(answers, a => Assert.Equal(AnswerSource.Bot, a.Source));
        Assert.All(answers, a => Assert.Equal(final.Id, a.MatchId));
        Assert.Equal(5, answers.Count(a => a.IsCorrect));
        Assert.Equal(10, mastery.Count); // ten different concepts in one deck
        Assert.All(mastery, m => Assert.Equal(1, m.AnswerCount));
        Assert.All(answers.Where(a => a.IsCorrect), a => Assert.True(a.MasteryAfter > a.MasteryBefore));
        Assert.All(answers.Where(a => !a.IsCorrect), a => Assert.True(a.MasteryAfter < a.MasteryBefore));
    }

    [Fact]
    public async Task Match_history_lists_finished_match()
    {
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");
        var final = await PlayToEndAsync(ana, await FirstQuestionAsync(ana, await StartBotAsync(ana, topic.Id)));

        var history = (await ana.Client.GetFromJsonAsync<MatchSummaryDto[]>("/api/me/matches", App.Json))!;

        var item = Assert.Single(history);
        Assert.Equal(final.Id, item.Id);
        Assert.Equal(MatchKind.Bot, item.Kind);
        Assert.Equal(topic.Id, item.Topic!.Id);
        Assert.Equal(SeatKind.Bot, item.Opponent!.Kind);
        Assert.Equal(BotPersona.Senior, item.Opponent.Persona);
        Assert.Equal(final.Result!.Mine.Score, item.MyScore);
        var expected = final.Result.WinnerSeat is null ? MatchOutcome.Draw : final.Result.WinnerSeat == 0 ? MatchOutcome.Win : MatchOutcome.Loss;
        Assert.Equal(expected, item.Outcome);
    }

    [Fact]
    public async Task Resign_finishes_with_the_bot_as_winner()
    {
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");
        var state = await FirstQuestionAsync(ana, await StartBotAsync(ana, topic.Id));

        var response = await ana.Client.PostAsync($"/api/matches/{state.Id}/resign", null);

        var final = (await ReadAsync<MatchStateDto>(response))!;
        Assert.Equal(MatchStatus.Finished, final.Status);
        Assert.Equal(MatchResultReason.Resign, final.Result!.Reason);
        Assert.Equal(1, final.Result.WinnerSeat);
        Assert.Null((await ana.Client.GetFromJsonAsync<MeDto>("/api/me", App.Json))!.ActiveMatchId);
    }

    [Fact]
    public async Task Practice_untimed_has_no_deadline_and_advances_on_next()
    {
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");

        var started = (await ReadAsync<MatchStateDto>(await PostAsync(ana.Client, "/api/matches/practice", new { topicId = topic.Id, timed = false })))!;

        Assert.Equal(MatchKind.Practice, started.Kind);
        Assert.Equal(MatchPhase.Question, started.Phase);
        Assert.Null(started.PhaseDeadline);
        Assert.Single(started.Players);
        var answered = (await ReadAsync<MatchStateDto>(await AnswerAsync(ana, started, Right(started), elapsedMs: 0)))!;
        Assert.Equal(MatchPhase.Interstitial, answered.Phase);
        Assert.Null(answered.PhaseDeadline);
        Assert.Null(answered.Interstitial!.Opponent);

        var next = (await ReadAsync<MatchStateDto>(await ana.Client.PostAsync($"/api/matches/{started.Id}/next", null)))!;

        Assert.Equal(MatchPhase.Question, next.Phase);
        Assert.Equal(1, next.Question!.Index);
    }

    [Fact]
    public async Task Practice_untimed_halves_mastery_weight()
    {
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");
        var started = (await ReadAsync<MatchStateDto>(await PostAsync(ana.Client, "/api/matches/practice", new { topicId = topic.Id, timed = false })))!;
        var question = started.Question!.Question;

        await AnswerAsync(ana, started, Right(started), elapsedMs: 0);

        var answer = await App.InDbAsync(db => db.Answers.SingleAsync());
        var weight = GameRules.DifficultyWeight[question.Difficulty] * GameRules.Types[question.Type].Guessability * GameRules.UntimedWeight;
        Assert.Equal(AnswerSource.Practice, answer.Source);
        Assert.Null(answer.TimeLimitMs);
        Assert.Equal(question.BasePoints, answer.Points); // untimed: no speed factor
        Assert.Equal(50 + 50 * weight, answer.MasteryAfter, 6);
    }

    [Fact]
    public async Task Practice_with_concept_ids_starts_with_them()
    {
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");
        var wanted = topic.Concepts.OrderBy(c => c.SortOrder).Last().Id;

        var started = (await ReadAsync<MatchStateDto>(await PostAsync(ana.Client, "/api/matches/practice", new { topicId = topic.Id, conceptIds = new[] { wanted } })))!;

        Assert.Equal(MatchPhase.Lobby, started.Phase); // timed practice counts down like a duel
        var first = await App.InDbAsync(db => db.MatchQuestions.SingleAsync(q => q.MatchId == started.Id && q.Index == 0));
        Assert.Equal(wanted, first.ConceptId);
        Assert.Equal(MatchSlot.Targeted, first.Slot);
    }

    [Fact]
    public async Task Next_in_a_timed_practice_interstitial_is_409()
    {
        var topic = await SeedTopicAsync();
        var ana = await RegisterAsync("ana");
        var started = (await ReadAsync<MatchStateDto>(await PostAsync(ana.Client, "/api/matches/practice", new { topicId = topic.Id, timed = true })))!;
        var state = await FirstQuestionAsync(ana, started);
        var answered = (await ReadAsync<MatchStateDto>(await AnswerAsync(ana, state, Right(state))))!;
        Assert.Equal(MatchPhase.Interstitial, answered.Phase); // solo: the interstitial opens as soon as I answer

        var response = await ana.Client.PostAsync($"/api/matches/{started.Id}/next", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
