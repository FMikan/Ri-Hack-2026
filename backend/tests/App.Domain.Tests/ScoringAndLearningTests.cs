using App.Domain.Content;
using App.Domain.Progress;
using static App.Domain.Tests.Fixtures;

namespace App.Domain.Tests;

public class ScoringTests
{
    [Fact]
    public void Full_time_remaining_gives_base_points() => Assert.Equal(100, Scoring.Points(100, 1, 0, 15_000, timed: true));

    [Fact]
    public void Half_time_remaining_gives_75_percent() => Assert.Equal(75, Scoring.Points(100, 1, 7_500, 15_000, timed: true));

    [Fact]
    public void No_time_remaining_gives_half_base() => Assert.Equal(50, Scoring.Points(100, 1, 15_000, 15_000, timed: true));

    [Fact]
    public void Wrong_answer_gives_zero() => Assert.Equal(0, Scoring.Points(100, 0, 1_000, 15_000, timed: true));

    [Fact]
    public void Partial_fraction_scales_points() => Assert.Equal(75, Scoring.Points(150, 0.5, 0, 30_000, timed: true));

    [Fact]
    public void Untimed_gives_base_times_fraction() => Assert.Equal(50, Scoring.Points(100, 0.5, 14_000, 15_000, timed: false));
}

public class GraderTests
{
    [Theory]
    [InlineData(QuestionType.TrueFalse, """{"value":true}""", """{"value":false}""")]
    [InlineData(QuestionType.MultipleChoice, """{"optionId":"b"}""", """{"optionId":"a"}""")]
    [InlineData(QuestionType.Cloze, """{"optionId":"c"}""", """{"optionId":"d"}""")]
    [InlineData(QuestionType.OddOneOut, """{"itemId":"d"}""", """{"itemId":"a"}""")]
    [InlineData(QuestionType.Output, """{"optionId":"a"}""", """{"optionId":"b"}""")]
    [InlineData(QuestionType.Order, """{"orderedIds":["s3","s1","s4","s2"]}""", """{"orderedIds":["s1","s3","s4","s2"]}""")]
    public void Single_answer_types_are_all_or_nothing(QuestionType type, string right, string wrong)
    {
        Assert.Equal(1, Grader.Grade(type, Answer(type), Json(right)));
        Assert.Equal(0, Grader.Grade(type, Answer(type), Json(wrong)));
    }

    [Fact]
    public void MatchPairs_fraction_is_correct_pairs_over_four()
    {
        var threeRight = """{"pairs":[{"leftId":"l1","rightId":"r3"},{"leftId":"l2","rightId":"r1"},{"leftId":"l3","rightId":"r4"},{"leftId":"l4","rightId":"r1"}]}""";
        var duplicateLeft = """{"pairs":[{"leftId":"l1","rightId":"r3"},{"leftId":"l1","rightId":"r3"}]}""";

        Assert.Equal(0.75, Grader.Grade(QuestionType.MatchPairs, Answer(QuestionType.MatchPairs), Json(threeRight)));
        Assert.Equal(0.25, Grader.Grade(QuestionType.MatchPairs, Answer(QuestionType.MatchPairs), Json(duplicateLeft)));
    }

    [Theory]
    [InlineData("""{"line":2,"fixId":"f2"}""", 0)]
    [InlineData("""{"line":3,"fixId":"f1"}""", 0.4)]
    [InlineData("""{"line":3,"fixId":"f2"}""", 1)]
    public void FindBug_needs_the_right_line_before_the_fix_counts(string payload, double expected) =>
        Assert.Equal(expected, Grader.Grade(QuestionType.FindBug, Answer(QuestionType.FindBug), Json(payload)));

    [Fact]
    public void FindBug_accepts_the_other_lines_of_a_split_statement()
    {
        var answer = Json("""{"line":4,"fixId":"f3","acceptLines":[3,5]}""");

        Assert.Equal(1, Grader.Grade(QuestionType.FindBug, answer, Json("""{"line":3,"fixId":"f3"}""")));
        Assert.Equal(0.4, Grader.Grade(QuestionType.FindBug, answer, Json("""{"line":5,"fixId":"f1"}""")));
        Assert.Equal(0, Grader.Grade(QuestionType.FindBug, answer, Json("""{"line":6,"fixId":"f3"}""")));
    }

    [Fact]
    public void Bot_never_taps_an_accepted_bug_line_when_it_answers_wrong()
    {
        const string content = """{"prompt":"P","code":"a\nb\nc\nd\ne","fixes":[{"id":"f1","text":"x"},{"id":"f2","text":"y"},{"id":"f3","text":"z"}],"answer":{"line":3,"fixId":"f2","acceptLines":[2,4]}}""";

        foreach (var seed in Enumerable.Range(0, 200))
        {
            var (payload, _) = Matches.BotPlayer.Answer(QuestionType.FindBug, content, 1, Matches.BotPersona.VibeCoder, 40_000, new Random(seed));
            var line = payload["line"]!.GetValue<int>();
            Assert.True(line == 3 || line is 1 or 5, $"seed {seed} tapped {line}");
        }
    }

    [Theory]
    [InlineData(QuestionType.MultipleChoice, """{"optionId":5}""")]
    [InlineData(QuestionType.MultipleChoice, "\"b\"")]
    [InlineData(QuestionType.MatchPairs, """{"pairs":"nope"}""")]
    [InlineData(QuestionType.MatchPairs, """{}""")]
    [InlineData(QuestionType.Order, """{"orderedIds":[1,2]}""")]
    [InlineData(QuestionType.FindBug, """{"fixId":"f2"}""")]
    public void Malformed_payload_is_zero_not_exception(QuestionType type, string payload) =>
        Assert.Equal(0, Grader.Grade(type, Answer(type), Json(payload)));

    [Fact]
    public void Public_content_has_the_type_and_no_answer()
    {
        var node = QuestionJson.Public(QuestionType.FindBug, ContentOf(QuestionType.FindBug));

        Assert.Equal("find_bug", node["type"]!.GetValue<string>());
        Assert.False(node.ContainsKey("answer"));
        Assert.Equal(3, node["fixes"]!.AsArray().Count);
    }
}

public class LabelTests
{
    [Fact]
    public void Brilliant_requires_D3_or_D4_correct_under_40_percent()
    {
        Assert.Equal(AnswerLabel.Brilliant, Labels.Of(3, 1, 5_000, 15_000, false, true));
        Assert.Equal(AnswerLabel.Good, Labels.Of(3, 1, 7_000, 15_000, false, true));
        Assert.Equal(AnswerLabel.Good, Labels.Of(2, 1, 1_000, 15_000, false, true));
    }

    [Fact]
    public void Partial_answer_is_good_from_half_and_miss_below()
    {
        Assert.Equal(AnswerLabel.Good, Labels.Of(3, 0.75, 1_000, 30_000, false, true));
        Assert.Equal(AnswerLabel.Miss, Labels.Of(3, 0.4, 1_000, 40_000, false, true));
    }

    [Fact]
    public void Wrong_answers_are_blunders_when_fast_or_on_D1()
    {
        Assert.Equal(AnswerLabel.Blunder, Labels.Of(2, 0, 3_000, 15_000, false, true));
        Assert.Equal(AnswerLabel.Blunder, Labels.Of(1, 0, 14_000, 15_000, false, true));
        Assert.Equal(AnswerLabel.Miss, Labels.Of(2, 0, 12_000, 15_000, false, true));
    }

    [Fact]
    public void Timeout_is_miss() => Assert.Equal(AnswerLabel.Miss, Labels.Of(1, 0, 15_000, 15_000, true, true));

    [Fact]
    public void Untimed_has_only_good_and_miss()
    {
        Assert.Equal(AnswerLabel.Good, Labels.Of(4, 1, 100, 15_000, false, false));
        Assert.Equal(AnswerLabel.Miss, Labels.Of(1, 0, 100, 15_000, false, false));
    }
}

public class MasteryTests
{
    [Fact]
    public void First_answer_uses_alpha_1()
    {
        var weight = Mastery.Weight(1, QuestionType.MultipleChoice, 1, AnswerLabel.Good, timed: true);

        Assert.Equal(76.25, Mastery.Next(50, 0, 100, weight), 6);
    }

    [Fact]
    public void Alpha_floor_is_015_after_six_answers()
    {
        Assert.Equal(57.5, Mastery.Next(50, 6, 100, 1), 6);
        Assert.Equal(50 + 50.0 / 6, Mastery.Next(50, 5, 100, 1), 6);
    }

    [Fact]
    public void Weight_combines_difficulty_guessability_blunder_and_untimed()
    {
        Assert.Equal(0.9, Mastery.Weight(2, QuestionType.MultipleChoice, 0, AnswerLabel.Miss, true), 6);
        Assert.Equal(0.675, Mastery.Weight(2, QuestionType.MultipleChoice, 1, AnswerLabel.Good, true), 6);
        Assert.Equal(0.9, Mastery.Weight(2, QuestionType.MatchPairs, 0.75, AnswerLabel.Good, true), 6);
        Assert.Equal(0.35, Mastery.Weight(1, QuestionType.TrueFalse, 1, AnswerLabel.Good, true), 6);
        Assert.Equal(0.84, Mastery.Weight(1, QuestionType.MultipleChoice, 0, AnswerLabel.Blunder, true), 6);
        Assert.Equal(0.2625, Mastery.Weight(1, QuestionType.MultipleChoice, 1, AnswerLabel.Good, false), 6);
    }

    [Fact]
    public void Partial_answer_mastery_uses_fraction()
    {
        var concept = Concept(Guid.Empty, 50, answers: 0);

        Learning(concept, 0.75, Mastery.Weight(2, QuestionType.MatchPairs, 0.75, AnswerLabel.Good, true));

        Assert.Equal(50 + 25 * 0.9, concept.Mastery, 6);
        Assert.Equal(1, concept.AnswerCount);
        Assert.Equal(Now, concept.LastAnsweredAt);
    }

    [Fact]
    public void Mastery_is_clamped_0_100()
    {
        Assert.Equal(0, Mastery.Next(10, 0, 0, 1.3));
        Assert.Equal(100, Mastery.Next(95, 0, 100, 1.3));
    }

    private static void Learning(ConceptMastery concept, double fraction, double weight) => Mastery.Apply(concept, fraction, weight, Now);
}

public class ConceptStateTests
{
    [Fact]
    public void States_follow_answers_and_mastery()
    {
        Assert.Equal(ConceptState.Unknown, ConceptStates.Of(null));
        Assert.Equal(ConceptState.Unknown, ConceptStates.Of(Concept(Guid.Empty, 10, answers: 0)));
        Assert.Equal(ConceptState.Weak, ConceptStates.Of(Concept(Guid.Empty, 39.9)));
        Assert.Equal(ConceptState.InProgress, ConceptStates.Of(Concept(Guid.Empty, 40)));
        Assert.Equal(ConceptState.Solid, ConceptStates.Of(Concept(Guid.Empty, 70)));
        var graduated = Concept(Guid.Empty, 30);
        graduated.IsGraduated = true;
        Assert.Equal(ConceptState.Graduated, ConceptStates.Of(graduated));
    }
}
