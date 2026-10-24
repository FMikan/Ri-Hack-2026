using App.Domain.Content;
using App.Domain.Matches;
using App.Domain.Progress;
using static App.Domain.Tests.Fixtures;

namespace App.Domain.Tests;

public class DeckBuilderTests
{
    private static readonly TopicPool Topic = Pool(12, 1, 2, 3);

    private static List<DeckSlot> Build(IReadOnlyList<DeckPlayer>? players = null, bool targeting = true, (int, int)? band = null, int seed = 1, TopicPool? pool = null, bool degrade = false) =>
        DeckBuilder.Build((pool ?? Topic).Deck, players ?? [Player()], targeting, band ?? (1, 2), Now, new Random(seed), degrade);

    private static Guid[] ConceptsIn(List<DeckSlot> deck, MatchSlot slot) =>
        deck.Where(s => s.Slot == slot).Select(s => s.Question.ConceptId).ToArray();

    [Theory]
    [InlineData(1000, 1000, 1, 2)]
    [InlineData(1350, 1420, 2, 3)] // the idea's example: average 1385 is D2, closer to the D3 threshold
    [InlineData(1150, 1150, 1, 2)]
    [InlineData(1500, 1520, 2, 3)]
    [InlineData(1650, 1690, 3, 4)]
    [InlineData(1800, 1900, 3, 4)]
    public void Band_pairs_neighbor_on_nearer_threshold_side(int a, int b, int min, int max) =>
        Assert.Equal((min, max), DeckBuilder.Band(a, b));

    [Fact]
    public void Deck_has_10_questions_first_is_true_false_D1()
    {
        var deck = Build();

        Assert.Equal(10, deck.Count);
        Assert.Equal(MatchSlot.Warmup, deck[0].Slot);
        Assert.Equal(QuestionType.TrueFalse, deck[0].Question.Type);
        Assert.Equal(1, deck[0].Question.Difficulty);
    }

    [Fact]
    public void Deck_respects_type_min_and_max_and_never_repeats_a_concept()
    {
        foreach (var seed in Enumerable.Range(0, 200))
        {
            var deck = Build(seed: seed);

            Assert.True(DeckBuilder.IsComplete(deck), $"seed {seed}");
            Assert.All(GameRules.Types, t => Assert.InRange(deck.Count(s => s.Question.Type == t.Key), t.Value.MinPerDeck, t.Value.MaxPerDeck));
            Assert.InRange(deck.Count(s => GameRules.DragAndDrop.Contains(s.Question.Type)), 1, 2);
            Assert.Equal(10, deck.Select(s => s.Question.ConceptId).Distinct().Count());
        }
    }

    [Fact]
    public void Targeted_slots_prefer_concepts_weak_for_both()
    {
        var c = Topic.Concepts;
        var a = Player(new() { [c[0]] = Concept(c[0], 20), [c[1]] = Concept(c[1], 30), [c[2]] = Concept(c[2], 80), [c[3]] = Concept(c[3], 50), [c[4]] = Concept(c[4], 35) });
        var b = Player(new() { [c[0]] = Concept(c[0], 20), [c[1]] = Concept(c[1], 30), [c[2]] = Concept(c[2], 30), [c[3]] = Concept(c[3], 60), [c[4]] = Concept(c[4], 35) });

        var deck = Build([a, b]);

        Assert.Equal(new[] { c[0], c[1], c[4] }.Order(), ConceptsIn(deck, MatchSlot.Targeted).Order());
    }

    [Fact]
    public void Targeted_slot_skips_concept_weak_for_one_strong_for_other()
    {
        var c = Topic.Concepts;
        var a = Player(new() { [c[0]] = Concept(c[0], 80), [c[1]] = Concept(c[1], 50) });
        var b = Player(new() { [c[0]] = Concept(c[0], 30), [c[1]] = Concept(c[1], 65) });

        var deck = Build([a, b]);

        Assert.Equal([c[1]], ConceptsIn(deck, MatchSlot.Targeted));
        Assert.Equal(10, deck.Count);
    }

    [Fact]
    public void Unknown_concepts_are_never_targeted()
    {
        var c = Topic.Concepts;
        var deck = Build([Player(new() { [c[0]] = Concept(c[0], 10, answers: 0) })]);

        Assert.Empty(ConceptsIn(deck, MatchSlot.Targeted));
    }

    [Fact]
    public void Targeted_slots_become_core_when_targeting_is_not_allowed()
    {
        var c = Topic.Concepts;
        var deck = Build([Player(new() { [c[0]] = Concept(c[0], 10) })], targeting: false);

        Assert.Empty(ConceptsIn(deck, MatchSlot.Targeted));
        Assert.Equal(10, deck.Count);
    }

    [Fact]
    public void Bot_match_targets_only_human_weak_concepts()
    {
        var c = Topic.Concepts;
        var human = Player(new() { [c[5]] = Concept(c[5], 15), [c[6]] = Concept(c[6], 75) });

        var deck = Build([human]);

        Assert.Equal([c[5]], ConceptsIn(deck, MatchSlot.Targeted));
    }

    [Fact]
    public void Review_slot_uses_due_concept_of_either_player()
    {
        var c = Topic.Concepts;
        var a = Player();
        var b = Player(new() { [c[7]] = Concept(c[7], 75, dueAt: Now.AddHours(-1)), [c[8]] = Concept(c[8], 75, dueAt: Now.AddDays(1)) });

        var deck = Build([a, b]);

        Assert.Equal([c[7]], ConceptsIn(deck, MatchSlot.Review));
    }

    [Fact]
    public void Questions_seen_in_last_30_days_are_skipped()
    {
        var seen = Topic.Questions.Where((_, i) => i % 2 == 0).Select(q => q.Id).ToHashSet();

        foreach (var seed in Enumerable.Range(0, 20))
            Assert.DoesNotContain(Build([Player(seen: seen)], seed: seed), s => seen.Contains(s.Question.Id));
    }

    [Fact]
    public void Missing_difficulty_falls_back_to_the_nearest()
    {
        var deck = Build(band: (3, 4), pool: Pool(12, 1, 2));

        Assert.Equal(10, deck.Count);
        Assert.All(deck.Skip(1), s => Assert.Equal(2, s.Question.Difficulty));
    }

    [Fact]
    public void Concept_targeted_in_last_two_matches_is_rotated_out()
    {
        var c = Topic.Concepts;
        var human = Player(new() { [c[0]] = Concept(c[0], 10), [c[1]] = Concept(c[1], 20) }, recentlyTargeted: [c[0]]);

        Assert.Equal([c[1]], ConceptsIn(Build([human]), MatchSlot.Targeted));
    }

    [Fact]
    public void Positions_2_to_10_are_shuffled_by_seed()
    {
        Assert.Equal(Build(seed: 7).Select(s => s.Question.Id), Build(seed: 7).Select(s => s.Question.Id));
        Assert.NotEqual(Build(seed: 7).Select(s => s.Question.Id), Build(seed: 8).Select(s => s.Question.Id));
    }

    [Fact]
    public void Bot_deck_degrades_instead_of_failing_on_thin_pool()
    {
        var thin = Pool(4, 1);
        var everythingSeen = thin.Questions.Select(q => q.Id).ToHashSet();

        var strict = Build([Player(seen: everythingSeen)], pool: thin);
        var degraded = Build([Player(seen: everythingSeen)], pool: thin, degrade: true);

        Assert.Empty(strict);
        Assert.Equal(10, degraded.Count);
        Assert.Equal(10, degraded.Select(s => s.Question.Id).Distinct().Count());
    }

    [Fact]
    public void Practice_deck_prefers_requested_then_due_then_weakest()
    {
        var c = Topic.Concepts;
        var player = Player(new()
        {
            [c[3]] = Concept(c[3], 60),
            [c[4]] = Concept(c[4], 20),
            [c[5]] = Concept(c[5], 80, dueAt: Now.AddMinutes(-5)),
        });

        var deck = DeckBuilder.BuildPractice(Topic.Deck, player, [c[9]], (1, 2), Now, new Random(3));

        Assert.Equal(10, deck.Count);
        Assert.Equal([c[9], c[5], c[4], c[3]], deck.Take(4).Select(s => s.Question.ConceptId));
        Assert.Equal(MatchSlot.Review, deck[1].Slot);
        Assert.DoesNotContain(deck, s => s.Slot == MatchSlot.Warmup);
    }

    [Fact]
    public void Practice_deck_repeats_concepts_on_a_thin_pool()
    {
        var thin = Pool(3, 1);

        var deck = DeckBuilder.BuildPractice(thin.Deck, Player(), [], (1, 2), Now, new Random(1));

        Assert.Equal(10, deck.Count);
        Assert.Equal(3, deck.Select(s => s.Question.ConceptId).Distinct().Count());
    }
}
