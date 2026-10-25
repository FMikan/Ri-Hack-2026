using System.Text.Json;
using App.Domain.Content;
using App.Domain.Matches;
using App.Domain.Progress;
using static App.Domain.Tests.Fixtures;

namespace App.Domain.Tests;

public class MatchEngineTests
{
    private static readonly TopicPool Topic = Pool(12, 1, 2);

    private static Match BotMatch(BotPersona persona = BotPersona.Senior, int seed = 5)
    {
        var deck = DeckBuilder.Build(Topic.Deck, [Player()], true, (1, 2), Now, new Random(seed), degrade: false);
        MatchPlayer[] players =
        [
            new() { Seat = 0, Kind = SeatKind.Human, UserId = Guid.NewGuid(), DisplayRating = 1000, LastSeenAt = Now },
            new() { Seat = 1, Kind = SeatKind.Bot, BotPersona = persona, DisplayRating = BotPlayer.Personas[persona].DisplayRating },
        ];
        return MatchEngine.Create(MatchKind.Bot, Guid.NewGuid(), true, (1, 2), seed, players, deck, Topic.ById, Now);
    }

    private static Match HumanMatch(bool timed = true, int humans = 2)
    {
        var deck = DeckBuilder.Build(Topic.Deck, [Player()], true, (1, 2), Now, new Random(1), degrade: false);
        var players = Enumerable.Range(0, humans).Select(seat => new MatchPlayer { Seat = seat, Kind = SeatKind.Human, UserId = Guid.NewGuid(), DisplayRating = 1000, LastSeenAt = Now });
        return MatchEngine.Create(humans == 1 ? MatchKind.Practice : MatchKind.Friend, Guid.NewGuid(), timed, (1, 2), 1, players, deck, Topic.ById, Now);
    }

    private static JsonElement KeyOf(Match match) => QuestionJson.Answer(match.Current.Question!.Content);

    private static JsonElement WrongOf(Match match) => Json("""{"nothing":true}""");

    private static DateTimeOffset ToQuestion(Match match)
    {
        var start = Now.AddSeconds(GameRules.LobbySeconds);
        MatchEngine.Advance(match, start);
        return start;
    }

    [Fact]
    public void Bot_match_starts_in_lobby_with_deck()
    {
        var match = BotMatch();

        Assert.Equal(MatchPhase.Lobby, match.Phase);
        Assert.Equal(Now.AddSeconds(5), match.PhaseDeadline);
        Assert.Equal(10, match.Questions.Count);
        Assert.Equal(Enumerable.Range(0, 10), match.Questions.Select(q => q.Index));
    }

    [Fact]
    public void Lobby_deadline_starts_question_0_with_the_bot_answer_written_ahead()
    {
        var match = BotMatch();

        var start = ToQuestion(match);

        Assert.Equal(MatchPhase.Question, match.Phase);
        Assert.Equal(0, match.CurrentIndex);
        Assert.Equal(start, match.QuestionStartedAt);
        Assert.Equal(start.AddMilliseconds(match.Current.TimeLimitMs + GameRules.ClientTimeToleranceMs), match.PhaseDeadline);
        var bot = match.AnswerOf(1, 0)!;
        Assert.True(bot.AnsweredAt > start);
        Assert.True(bot.ElapsedMs <= match.Current.TimeLimitMs - GameRules.BotMinSlackMs);
        Assert.False(match.IsRevealed(0));
    }

    [Fact]
    public void Human_answer_before_the_bot_sets_the_deadline_to_the_bot_answer()
    {
        var match = BotMatch();
        var start = ToQuestion(match);
        var bot = match.AnswerOf(1, 0)!;

        MatchEngine.SubmitAnswer(match, 0, 0, KeyOf(match), 500, start.AddMilliseconds(500));

        Assert.Equal(MatchPhase.Question, match.Phase);
        Assert.Equal(bot.AnsweredAt, match.PhaseDeadline);
        MatchEngine.Advance(match, bot.AnsweredAt);
        Assert.Equal(MatchPhase.Interstitial, match.Phase);
        Assert.True(match.IsRevealed(0));
    }

    [Fact]
    public void Both_answered_moves_to_interstitial_immediately()
    {
        var match = BotMatch();
        var start = ToQuestion(match);
        var late = match.AnswerOf(1, 0)!.AnsweredAt.AddMilliseconds(100);

        var recorded = MatchEngine.SubmitAnswer(match, 0, 0, KeyOf(match), (int)(late - start).TotalMilliseconds, late);

        Assert.Equal(MatchPhase.Interstitial, match.Phase);
        Assert.Equal(late.AddSeconds(GameRules.BotInterstitialSeconds), match.PhaseDeadline);
        Assert.True(recorded.Answer.IsCorrect);
        Assert.True(recorded.CountsForLearning);
    }

    [Fact]
    public void Timeout_writes_zero_answer_and_moves_on()
    {
        var match = BotMatch();
        var start = ToQuestion(match);
        var deadline = match.PhaseDeadline!.Value;

        var recorded = MatchEngine.Advance(match, deadline);

        Assert.Equal(MatchPhase.Interstitial, match.Phase);
        var timeout = Assert.Single(recorded);
        Assert.True(timeout.Answer.TimedOut);
        Assert.Equal(0, timeout.Answer.Points);
        Assert.Equal(AnswerLabel.Miss, timeout.Answer.Label);
        Assert.True(timeout.CountsForLearning); // last seen at creation, still within the grace period
        Assert.True(deadline - start < TimeSpan.FromSeconds(GameRules.ReconnectGraceSeconds + 30));
    }

    [Fact]
    public void Client_elapsed_outside_tolerance_is_replaced_by_server_elapsed()
    {
        var trusted = BotMatch();
        var start = ToQuestion(trusted);
        Assert.Equal(5_000, MatchEngine.SubmitAnswer(trusted, 0, 0, KeyOf(trusted), 5_000, start.AddMilliseconds(6_000)).Answer.ElapsedMs);

        var replaced = BotMatch();
        start = ToQuestion(replaced);
        Assert.Equal(6_000, MatchEngine.SubmitAnswer(replaced, 0, 0, KeyOf(replaced), 1_000, start.AddMilliseconds(6_000)).Answer.ElapsedMs);
    }

    [Fact]
    public void Delivered_time_is_the_reference_when_known()
    {
        var match = BotMatch();
        var start = ToQuestion(match);
        match.Players[0].DeliveredIndex = 0;
        match.Players[0].DeliveredAt = start.AddMilliseconds(900);

        var answer = MatchEngine.SubmitAnswer(match, 0, 0, KeyOf(match), 100, start.AddMilliseconds(1_000)).Answer;

        Assert.Equal(100, answer.ElapsedMs);
    }

    [Fact]
    public void Second_answer_late_answer_and_wrong_index_are_rejected()
    {
        var match = BotMatch();
        var start = ToQuestion(match);

        Assert.Equal(ErrorCodes.MatchWrongIndex, Assert.Throws<AppException>(() => MatchEngine.SubmitAnswer(match, 0, 1, KeyOf(match), 100, start)).Code);
        Assert.Equal(ErrorCodes.MatchWrongPhase, Assert.Throws<AppException>(() =>
            MatchEngine.SubmitAnswer(match, 0, 0, KeyOf(match), 100, match.PhaseDeadline!.Value.AddMilliseconds(1))).Code);
        MatchEngine.SubmitAnswer(match, 0, 0, KeyOf(match), 100, start.AddMilliseconds(100));
        Assert.Equal(ErrorCodes.MatchAlreadyAnswered, Assert.Throws<AppException>(() => MatchEngine.SubmitAnswer(match, 0, 0, KeyOf(match), 100, start.AddMilliseconds(200))).Code);
    }

    [Fact]
    public void Answer_in_interstitial_is_rejected()
    {
        var match = BotMatch();
        MatchEngine.Advance(match, ToQuestion(match).AddMinutes(1).AddSeconds(-59)); // still in question 0
        MatchEngine.Advance(match, match.PhaseDeadline!.Value);

        Assert.Equal(MatchPhase.Interstitial, match.Phase);
        Assert.Equal(ErrorCodes.MatchWrongPhase, Assert.Throws<AppException>(() => MatchEngine.SubmitAnswer(match, 0, 0, KeyOf(match), 100, match.PhaseDeadline!.Value.AddMilliseconds(-1))).Code);
    }

    [Fact]
    public void Untimed_practice_starts_at_once_and_waits_for_next()
    {
        var match = HumanMatch(timed: false, humans: 1);

        Assert.Equal(MatchPhase.Question, match.Phase);
        Assert.Null(match.PhaseDeadline);
        MatchEngine.SubmitAnswer(match, 0, 0, KeyOf(match), 60_000, Now.AddMinutes(1));
        Assert.Equal(MatchPhase.Interstitial, match.Phase);
        Assert.Null(match.PhaseDeadline);
        Assert.Empty(MatchEngine.Advance(match, Now.AddHours(1)));
        Assert.Equal(MatchPhase.Interstitial, match.Phase);

        MatchEngine.Next(match, Now.AddMinutes(2));

        Assert.Equal(1, match.CurrentIndex);
        Assert.Equal(MatchPhase.Question, match.Phase);
    }

    [Fact]
    public void Bot_duel_interstitial_lasts_30_s_or_until_next()
    {
        var match = BotMatch();
        var start = ToQuestion(match);
        var late = match.AnswerOf(1, 0)!.AnsweredAt.AddMilliseconds(100);
        MatchEngine.SubmitAnswer(match, 0, 0, KeyOf(match), (int)(late - start).TotalMilliseconds, late);
        Assert.Equal(late.AddSeconds(GameRules.BotInterstitialSeconds), match.PhaseDeadline);

        MatchEngine.Next(match, late.AddSeconds(3));

        Assert.Equal(1, match.CurrentIndex);
        Assert.Equal(MatchPhase.Question, match.Phase);
        Assert.Equal(late.AddSeconds(3), match.QuestionStartedAt);
    }

    [Fact]
    public void Next_is_rejected_in_timed_duels()
    {
        var match = HumanMatch();
        var start = ToQuestion(match);
        MatchEngine.SubmitAnswer(match, 0, 0, KeyOf(match), 500, start.AddMilliseconds(500));
        MatchEngine.SubmitAnswer(match, 1, 0, KeyOf(match), 600, start.AddMilliseconds(600));
        Assert.Equal(MatchPhase.Interstitial, match.Phase);
        Assert.Equal(start.AddMilliseconds(600).AddSeconds(GameRules.InterstitialSeconds), match.PhaseDeadline);

        Assert.Equal(ErrorCodes.MatchWrongPhase, Assert.Throws<AppException>(() => MatchEngine.Next(match, start.AddSeconds(1))).Code);
    }

    [Fact]
    public void Answering_every_question_finishes_with_totals_and_a_winner()
    {
        var match = BotMatch(BotPersona.VibeCoder);
        var human = match.Players[0];
        var now = ToQuestion(match);
        while (match.Phase != MatchPhase.Done)
        {
            now = now.AddMilliseconds(300);
            MatchEngine.SubmitAnswer(match, 0, match.CurrentIndex, KeyOf(match), 300, now);
            now = match.PhaseDeadline!.Value;
            MatchEngine.Advance(match, now);
            now = match.PhaseDeadline ?? now;
            human.LastSeenAt = now; // the client keeps polling through the 30 s interstitial
            MatchEngine.Advance(match, now);
        }

        Assert.Equal(10, human.CorrectCount);
        Assert.Equal(match.Answers.Where(a => a.Seat == 0).Sum(a => a.Points), human.Score);
        Assert.Equal(0, match.WinnerSeat);
        Assert.Equal(MatchResultReason.Score, match.ResultReason);
        Assert.Equal(20, match.Answers.Count);
    }

    [Fact]
    public void Disconnected_player_at_finish_loses_and_late_timeouts_do_not_teach()
    {
        var match = BotMatch();
        var recorded = MatchEngine.Advance(match, Now.AddHours(1));

        Assert.Equal(MatchPhase.Done, match.Phase);
        Assert.Equal(1, match.WinnerSeat);
        Assert.Equal(MatchResultReason.Disconnect, match.ResultReason);
        Assert.Equal(10, recorded.Count);
        Assert.True(recorded[0].CountsForLearning);
        Assert.False(recorded[^1].CountsForLearning);
    }

    [Fact]
    public void Resign_finishes_with_opponent_as_winner()
    {
        var match = BotMatch();
        var start = ToQuestion(match);

        MatchEngine.Resign(match, 0, start.AddSeconds(1));

        Assert.Equal(MatchPhase.Done, match.Phase);
        Assert.Equal(1, match.WinnerSeat);
        Assert.Equal(MatchResultReason.Resign, match.ResultReason);
        Assert.Equal(0, match.Players[1].Score); // the bot's answer to the open question had not arrived yet
        Assert.Throws<AppException>(() => MatchEngine.Resign(match, 0, start.AddSeconds(2)));
    }

    [Fact]
    public void Both_disconnected_aborts()
    {
        var match = HumanMatch();

        MatchEngine.Advance(match, Now.AddHours(1));

        Assert.Equal(MatchResultReason.Aborted, match.ResultReason);
        Assert.Null(match.WinnerSeat);
    }

    [Fact]
    public void Wrong_answer_scores_zero_and_is_labelled()
    {
        var match = BotMatch();
        var start = ToQuestion(match);

        var answer = MatchEngine.SubmitAnswer(match, 0, 0, WrongOf(match), 1_000, start.AddSeconds(1)).Answer;

        Assert.False(answer.IsCorrect);
        Assert.Equal(0, answer.Points);
        Assert.Equal(AnswerLabel.Blunder, answer.Label); // fast and the warmup is D1
    }
}

public class TiebreakTests
{
    private static MatchPlayer P(int seat, int score, int correct, int time) =>
        new() { Seat = seat, Kind = SeatKind.Human, DisplayRating = 1000, Score = score, CorrectCount = correct, TimeOnCorrectMs = time };

    [Fact]
    public void Points_then_correct_answers_then_time_then_draw()
    {
        Assert.Equal((1, MatchResultReason.Score), Tiebreak.Of(P(0, 500, 9, 1), P(1, 600, 1, 1)));
        Assert.Equal((0, MatchResultReason.TiebreakCorrect), Tiebreak.Of(P(0, 500, 6, 9_000), P(1, 500, 5, 1_000)));
        Assert.Equal((1, MatchResultReason.TiebreakTime), Tiebreak.Of(P(0, 500, 5, 9_000), P(1, 500, 5, 8_000)));
        Assert.Equal((null, MatchResultReason.Draw), Tiebreak.Of(P(0, 500, 5, 9_000), P(1, 500, 5, 9_000)));
    }
}

public class ResultCardTests
{
    [Fact]
    public void Blunder_is_costliest_miss_brilliant_is_best_answer_practice_is_biggest_drop()
    {
        var match = new Match { Kind = MatchKind.Bot, RngSeed = 1, CreatedAt = Now };
        (int Base, double Fraction, int Points, AnswerLabel Label)[] answers =
        [
            (50, 1, 50, AnswerLabel.Good),
            (200, 0.4, 60, AnswerLabel.Miss),
            (150, 0, 0, AnswerLabel.Miss),
            (100, 1, 95, AnswerLabel.Brilliant),
            (120, 1, 110, AnswerLabel.Good),
        ];
        for (var i = 0; i < answers.Length; i++)
        {
            match.Questions.Add(new MatchQuestion { Index = i, QuestionId = Guid.NewGuid(), ConceptId = Guid.NewGuid(), Slot = MatchSlot.Core, Type = QuestionType.MultipleChoice, Difficulty = 3, BasePoints = answers[i].Base, TimeLimitMs = 15_000 });
            match.Answers.Add(new MatchAnswer { Seat = 0, Index = i, IsCorrect = answers[i].Fraction >= 1, Fraction = answers[i].Fraction, Points = answers[i].Points, ElapsedMs = 1, AnsweredAt = Now, Label = answers[i].Label });
        }
        var dropped = Guid.NewGuid();

        var cards = ResultCardRules.Of(match, 0, new Dictionary<Guid, double> { [Guid.NewGuid()] = -3, [dropped] = -12, [Guid.NewGuid()] = 8 });

        Assert.Equal(2, cards.BlunderIndex); // 150 lost beats 120 lost on the partial find-the-bug
        Assert.Equal(3, cards.BrilliantIndex);
        Assert.Equal(dropped, cards.PracticeConceptId);
        Assert.Equal(-12, cards.PracticeConceptDelta);
    }

    [Fact]
    public void Cards_are_null_when_nothing_qualifies()
    {
        var cards = ResultCardRules.Of(new Match { Kind = MatchKind.Bot, RngSeed = 1, CreatedAt = Now }, 0, new Dictionary<Guid, double>());

        Assert.Equal(new ResultCards(null, null, null, 0), cards);
    }
}

public class BotPlayerTests
{
    [Fact]
    public void Accuracy_drops_8_percent_per_difficulty_above_2()
    {
        Assert.Equal(0.75, BotPlayer.AccuracyFor(BotPersona.Mid, 2), 6);
        Assert.Equal(0.59, BotPlayer.AccuracyFor(BotPersona.Mid, 4), 6);
        Assert.Equal(0.58, BotPlayer.AccuracyFor(BotPersona.VibeCoder, 1), 6);
    }

    [Theory]
    [MemberData(nameof(AllTypes))]
    public void Bot_answers_are_either_fully_right_or_worth_zero(QuestionType type)
    {
        var results = Enumerable.Range(0, 200).Select(seed =>
        {
            var (payload, _) = BotPlayer.Answer(type, ContentOf(type), 2, BotPersona.VibeCoder, GameRules.Types[type].TimeMs, new Random(seed));
            return Grader.Grade(type, Answer(type), Json(payload.ToJsonString()));
        }).ToList();

        Assert.All(results, r => Assert.True(r is 0 or 1, $"{type} gave {r}"));
        Assert.Contains(1.0, results);
        Assert.Contains(0.0, results);
    }

    [Fact]
    public void Elapsed_never_exceeds_time_limit_minus_slack()
    {
        foreach (var seed in Enumerable.Range(0, 100))
        {
            var (_, elapsed) = BotPlayer.Answer(QuestionType.TrueFalse, ContentOf(QuestionType.TrueFalse), 1, BotPersona.Junior, 8_000, new Random(seed));
            Assert.InRange(elapsed, 3_000, 7_500);
        }
    }

    [Fact]
    public void Recommended_persona_is_closest_to_rating()
    {
        Assert.Equal(BotPersona.VibeCoder, BotPlayer.Recommended(1000));
        Assert.Equal(BotPersona.Junior, BotPlayer.Recommended(1250));
        Assert.Equal(BotPersona.Senior, BotPlayer.Recommended(2100));
    }

    public static TheoryData<QuestionType> AllTypes() => [.. Enum.GetValues<QuestionType>()];
}

public class RankedVetoFlowTests
{
    private static readonly TopicPool Topic = Pool(12, 1, 2);
    private static readonly Guid[] T = Enumerable.Range(1, 4).Select(i => new Guid(i, 0, 0, new byte[8])).ToArray();

    private static Match Ranked(int coinWinner = 0)
    {
        MatchPlayer[] players =
        [
            new() { Seat = 0, Kind = SeatKind.Human, UserId = Guid.NewGuid(), DisplayRating = 1000 },
            new() { Seat = 1, Kind = SeatKind.Human, UserId = Guid.NewGuid(), DisplayRating = 1040 },
        ];
        var veto = new VetoState { Pool = [.. T], Picks = [[T[0], T[1], T[2]], [T[0], T[3], T[1]]], CoinWinnerSeat = coinWinner, TurnSeat = coinWinner };
        return MatchEngine.CreateRanked(players, veto, seed: 3, Now);
    }

    private static void AttachDeck(Match match) =>
        MatchEngine.AttachDeck(match, (1, 2), DeckBuilder.Build(Topic.Deck, [Player()], true, (1, 2), Now, new Random(1), degrade: false), Topic.ById);

    [Fact]
    public void Ranked_match_starts_in_veto_coin_without_topic_or_deck()
    {
        var match = Ranked();

        Assert.Equal(MatchKind.Ranked, match.Kind);
        Assert.Equal(MatchPhase.VetoCoin, match.Phase);
        Assert.Equal(Now.AddSeconds(GameRules.VetoAnimationSeconds), match.PhaseDeadline);
        Assert.Null(match.TopicId);
        Assert.Empty(match.Questions);
        Assert.All(match.Players, p => Assert.Equal(Now, p.LastSeenAt));
    }

    [Fact]
    public void Coin_animation_opens_the_first_ban_for_the_coin_winner()
    {
        var match = Ranked(coinWinner: 1);

        MatchEngine.Advance(match, Now.AddSeconds(3));

        Assert.Equal(MatchPhase.VetoBan, match.Phase);
        Assert.Equal(1, match.Veto!.TurnSeat);
        Assert.Equal(Now.AddSeconds(3 + GameRules.VetoBanSeconds), match.PhaseDeadline);
    }

    [Fact]
    public void Bans_pass_the_turn_and_the_last_one_picks_the_topic()
    {
        var match = Ranked();
        MatchEngine.Advance(match, Now.AddSeconds(3));

        MatchEngine.Ban(match, 0, T[2], Now.AddSeconds(5));
        Assert.Equal(Now.AddSeconds(5 + GameRules.VetoBanSeconds), match.PhaseDeadline);
        MatchEngine.Ban(match, 1, T[3], Now.AddSeconds(7));

        Assert.Equal(MatchPhase.VetoPick, match.Phase);
        Assert.Contains(match.Veto!.ChosenTopicId!.Value, new[] { T[0], T[1] });
        Assert.Equal(match.Veto.ChosenTopicId, match.TopicId);
        Assert.Equal(Now.AddSeconds(7 + GameRules.VetoAnimationSeconds), match.PhaseDeadline);
    }

    [Fact]
    public void Ban_outside_the_ban_phase_is_rejected()
    {
        var match = Ranked();

        Assert.Equal(ErrorCodes.MatchWrongPhase, Assert.Throws<AppException>(() => MatchEngine.Ban(match, 0, T[2], Now.AddSeconds(1))).Code);
    }

    [Fact]
    public void Ban_timeout_auto_bans_and_passes_turn()
    {
        var match = Ranked();

        MatchEngine.Advance(match, Now.AddSeconds(3 + GameRules.VetoBanSeconds));

        var ban = Assert.Single(match.Veto!.Bans);
        Assert.True(ban.Auto);
        Assert.Equal(0, ban.Seat);
        Assert.Equal(1, match.Veto.TurnSeat);
        Assert.Equal(MatchPhase.VetoBan, match.Phase);
    }

    [Fact]
    public void Pick_waits_for_the_deck_then_goes_to_the_lobby_and_the_first_question()
    {
        var match = Ranked();
        var late = Now.AddSeconds(60); // a late tick runs every passed deadline, but stops where the deck is needed

        MatchEngine.Advance(match, late);
        Assert.Equal(MatchPhase.VetoPick, match.Phase);
        Assert.Equal(2, match.Veto!.Bans.Count);

        AttachDeck(match);
        MatchEngine.Advance(match, late);

        // coin 3 s + two auto-bans of 8 s + pick 3 s + lobby 5 s: the first question started at 27 s.
        Assert.Equal(Now.AddSeconds(27), match.StartedAt);
        Assert.Contains(match.Phase, new[] { MatchPhase.Question, MatchPhase.Interstitial });
        Assert.Equal(10, match.Questions.Count);
        Assert.Equal((1, 2), (match.BandMin, match.BandMax));
    }
}
