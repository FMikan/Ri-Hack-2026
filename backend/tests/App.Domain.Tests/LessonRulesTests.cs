using App.Domain.Content;
using App.Domain.Lessons;
using App.Domain.Matches;
using App.Domain.Progress;
using static App.Domain.Tests.Fixtures;

namespace App.Domain.Tests;

public class LessonRulesTests
{
    private static readonly Guid A = Id(1), B = Id(2), C = Id(3), D = Id(4), E = Id(5), F = Id(6);

    private static Guid Id(int n) => new(n, 0, 0, new byte[8]);

    private static DeckQuestion Q(int n, Guid concept, QuestionType type, int difficulty) => new(Id(100 + n), concept, type, difficulty);

    [Fact]
    public void Concepts_are_the_misses_costliest_first_then_up_to_two_chronic_weak_ones()
    {
        var misses = new[] { (A, 50.0), (B, 200.0), (C, 100.0), (D, 75.0) };
        var mastery = new[]
        {
            Concept(E, 30), Concept(F, 10), Concept(Id(7), 35), Concept(B, 5),
            Concept(A, 2), // missed in this match but cut by the limit: not a chronic gap
            Concept(Id(8), 20, answers: 0), // unknown, not chronic
        };

        var concepts = LessonRules.Concepts(misses, mastery);

        Assert.Equal([B, C, D, F, E], concepts);
    }

    [Fact]
    public void Concepts_keep_the_variant_concept_even_when_it_is_cheap()
    {
        var concepts = LessonRules.Concepts([(A, 50.0), (B, 200.0), (C, 100.0), (D, 75.0)], [], first: A);

        Assert.Equal([A, B, C], concepts);
    }

    [Fact]
    public void Variant_is_same_concept_same_difficulty_other_type()
    {
        var missed = Q(0, A, QuestionType.MultipleChoice, 2);
        DeckQuestion[] pool = [missed, Q(1, A, QuestionType.MultipleChoice, 2), Q(2, A, QuestionType.Output, 3), Q(3, A, QuestionType.Cloze, 2), Q(4, B, QuestionType.Order, 2)];

        Assert.Equal(Id(103), LessonRules.Variant(pool, missed, seen: new HashSet<Guid>(), exclude: new HashSet<Guid>(), new Random(1))!.Id);
    }

    [Fact]
    public void Variant_falls_back_to_neighbor_difficulty()
    {
        var missed = Q(0, A, QuestionType.MultipleChoice, 2);
        DeckQuestion[] pool = [missed, Q(1, A, QuestionType.MultipleChoice, 2), Q(2, A, QuestionType.Output, 3), Q(3, A, QuestionType.Cloze, 2)];

        var variant = LessonRules.Variant(pool, missed, seen: new HashSet<Guid> { Id(103) }, exclude: new HashSet<Guid>(), new Random(1));

        Assert.Equal(Id(102), variant!.Id);
    }

    [Fact]
    public void Variant_falls_back_to_the_same_type_then_to_seen_questions()
    {
        var missed = Q(0, A, QuestionType.MultipleChoice, 2);
        DeckQuestion[] pool = [missed, Q(1, A, QuestionType.MultipleChoice, 2), Q(2, A, QuestionType.Output, 2)];

        Assert.Equal(Id(101), LessonRules.Variant(pool, missed, new HashSet<Guid> { Id(102) }, new HashSet<Guid>(), new Random(1))!.Id);
        Assert.Equal(Id(102), LessonRules.Variant(pool, missed, new HashSet<Guid> { Id(101), Id(102) }, new HashSet<Guid>(), new Random(1))!.Id);
        Assert.Null(LessonRules.Variant([missed], missed, new HashSet<Guid>(), new HashSet<Guid>(), new Random(1)));
    }

    [Fact]
    public void Other_checks_are_unseen_first_then_spread_over_other_lesson_concepts()
    {
        var variant = Q(0, A, QuestionType.Cloze, 2);
        DeckQuestion[] pool =
        [
            variant,
            Q(1, A, QuestionType.Output, 2), // same concept as the variant
            Q(2, B, QuestionType.Output, 2),
            Q(3, C, QuestionType.Order, 2), // seen
            Q(4, D, QuestionType.Order, 2), // not a lesson concept
            Q(5, C, QuestionType.TrueFalse, 3), // unseen, one step outside the band
        ];

        var checks = LessonRules.OtherChecks(pool, [A, B, C], variant, seen: new HashSet<Guid> { Id(103) }, exclude: new HashSet<Guid>(), band: (1, 2), new Random(1));

        Assert.Equal([Id(102), Id(105)], checks.Select(q => q.Id));
    }

    [Theory]
    [InlineData(true, 1, true)]
    [InlineData(true, 2, true)]
    [InlineData(true, 0, false)]
    [InlineData(false, 2, false)]
    public void Lesson_pass_requires_variant_plus_one_more(bool variantCorrect, int othersCorrect, bool passed) =>
        Assert.Equal(passed, LessonRules.Passed(variantCorrect, othersCorrect));

    [Fact]
    public void Reward_is_for_the_first_lesson_of_a_match_on_a_day_without_an_earlier_attempt()
    {
        Lesson New(bool first = true) => new() { UserId = A, MatchId = B, FirstForMatch = first, ConceptIds = [C], Cards = "[]", CardsSource = LessonCardsSource.Fallback, CheckQuestionIds = [], CreatedAt = Now };
        var lesson = New();

        Assert.True(LessonRules.RewardEligible(lesson, Now));
        lesson.LastAttemptAt = Now.AddMinutes(-20);
        Assert.False(LessonRules.RewardEligible(lesson, Now)); // failed earlier today
        Assert.True(LessonRules.RewardEligible(lesson, Now.AddDays(1)));
        Assert.False(LessonRules.RewardEligible(New(first: false), Now)); // a second lesson for the same match
        lesson.Rewarded = true;
        Assert.False(LessonRules.RewardEligible(lesson, Now.AddDays(1)));
    }
}
