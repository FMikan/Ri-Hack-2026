using App.Domain.Content;
using App.Domain.Progress;

namespace App.Domain.Matches;

public record DeckQuestion(Guid Id, Guid ConceptId, QuestionType Type, int Difficulty);

public record DeckSlot(DeckQuestion Question, MatchSlot Slot);

// What the deck builder knows about one human player.
public record DeckPlayer(
    IReadOnlyDictionary<Guid, ConceptMastery> Mastery,
    IReadOnlySet<Guid> SeenQuestionIds,
    IReadOnlySet<Guid> RecentlyTargetedConceptIds);

// Idea §8: warmup (1) + targeted (3) + core (4) + review (2); one concept at most once; type limits from GameRules.
public static class DeckBuilder
{
    private const int Targeted = 3, Review = 2;

    public static int DifficultyOf(double rating) => 1 + GameRules.DifficultyThresholds.Count(t => rating >= t);

    // Two neighbouring difficulties on the side of the nearer threshold: 1350 + 1420 (avg 1385, D2) → D2–D3.
    public static (int Min, int Max) Band(int ratingA, int ratingB)
    {
        var average = (ratingA + ratingB) / 2.0;
        var d = DifficultyOf(average);
        if (d == 1) return (1, 2);
        if (d == 4) return (3, 4);
        var lower = GameRules.DifficultyThresholds[d - 2];
        var upper = GameRules.DifficultyThresholds[d - 1];
        return upper - average <= average - lower ? (d, d + 1) : (d - 1, d);
    }

    // Ranked, ghost, friend and bot matches. targetingAllowed is false when a ranked player has no profile yet.
    // degrade lets bot matches play on a thin pool (seen questions, then repeated concepts) instead of failing.
    public static List<DeckSlot> Build(
        IReadOnlyList<DeckQuestion> pool, IReadOnlyList<DeckPlayer> players, bool targetingAllowed,
        (int Min, int Max) band, DateTimeOffset now, Random rng, bool degrade)
    {
        var seen = players.SelectMany(p => p.SeenQuestionIds).ToHashSet();
        var draft = new Draft(pool, seen, band, rng, enforceMinimums: true);
        var concepts = pool.Select(q => q.ConceptId).Distinct().ToList();

        var warmup = Lowest(pool.Where(q => q.Type == QuestionType.TrueFalse && !seen.Contains(q.Id)), rng)
            ?? (degrade ? Lowest(pool.Where(q => q.Type == QuestionType.TrueFalse), rng) : null);
        if (warmup is not null) draft.Add(warmup, MatchSlot.Warmup);

        if (targetingAllowed && players.Count > 0)
            draft.AddUpTo(TargetCandidates(players, concepts), MatchSlot.Targeted, Targeted);
        draft.AddUpTo(DueConcepts(players, concepts, now), MatchSlot.Review, Review);
        draft.AddUpTo(Shuffle(concepts, rng), MatchSlot.Core, GameRules.QuestionsPerMatch - draft.Slots.Count);
        draft.RepairMinimums(allowSeen: degrade);
        if (degrade) draft.Degrade(concepts);

        var first = warmup is null ? 0 : 1;
        var rest = draft.Slots.Skip(first).ToList();
        rng.Shuffle(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(rest));
        return [.. draft.Slots.Take(first), .. rest];
    }

    // Practice (idea §12): requested concepts, then due reviews, then the weakest answered concepts, then unknown ones.
    public static List<DeckSlot> BuildPractice(
        IReadOnlyList<DeckQuestion> pool, DeckPlayer player, IReadOnlyCollection<Guid> requestedConceptIds,
        (int Min, int Max) band, DateTimeOffset now, Random rng)
    {
        var draft = new Draft(pool, player.SeenQuestionIds, band, rng, enforceMinimums: false);
        var concepts = pool.Select(q => q.ConceptId).Distinct().ToList();
        var due = DueConcepts([player], concepts, now).ToList();
        var answered = concepts
            .Where(c => player.Mastery.GetValueOrDefault(c) is { AnswerCount: > 0 })
            .OrderBy(c => player.Mastery[c].Mastery);
        var unknown = Shuffle(concepts.Where(c => player.Mastery.GetValueOrDefault(c) is not { AnswerCount: > 0 }).ToList(), rng);

        draft.AddUpTo(requestedConceptIds.Where(concepts.Contains), MatchSlot.Targeted, GameRules.QuestionsPerMatch);
        draft.AddUpTo(due, MatchSlot.Review, GameRules.QuestionsPerMatch - draft.Slots.Count);
        draft.AddUpTo(answered, MatchSlot.Targeted, GameRules.QuestionsPerMatch - draft.Slots.Count);
        draft.AddUpTo(unknown, MatchSlot.Core, GameRules.QuestionsPerMatch - draft.Slots.Count);
        draft.Degrade(concepts);
        return draft.Slots;
    }

    // A ranked deck must be complete: 10 questions and every type minimum met.
    public static bool IsComplete(IReadOnlyList<DeckSlot> deck) =>
        deck.Count == GameRules.QuestionsPerMatch
        && GameRules.Types.All(t => deck.Count(s => s.Question.Type == t.Key) >= t.Value.MinPerDeck)
        && deck.Count(s => GameRules.DragAndDrop.Contains(s.Question.Type)) >= GameRules.DragAndDropMin;

    // Weak for every player first, then concepts every player knows about equally (gap ≤ 20, all < 70); lowest average first.
    // A concept nobody answered yet (unknown) is never targeted, and one targeted in the last two matches rests.
    private static IEnumerable<Guid> TargetCandidates(IReadOnlyList<DeckPlayer> players, IEnumerable<Guid> concepts) =>
        concepts
            .Where(c => players.All(p => p.Mastery.GetValueOrDefault(c) is { AnswerCount: > 0 } && !p.RecentlyTargetedConceptIds.Contains(c)))
            .Select(c => (Concept: c, Values: players.Select(p => p.Mastery[c].Mastery).ToList()))
            .Select(x => (x.Concept, x.Values, Group:
                x.Values.All(v => v < GameRules.WeakMastery) ? 0
                : x.Values.All(v => v < GameRules.SolidMastery) && x.Values.Max() - x.Values.Min() <= GameRules.TargetedMaxGap ? 1
                : -1))
            .Where(x => x.Group >= 0)
            .OrderBy(x => x.Group).ThenBy(x => x.Values.Average())
            .Select(x => x.Concept);

    private static IEnumerable<Guid> DueConcepts(IReadOnlyList<DeckPlayer> players, IEnumerable<Guid> concepts, DateTimeOffset now) =>
        concepts
            .Select(c => (Concept: c, Due: players.Select(p => p.Mastery.GetValueOrDefault(c))
                .Where(m => m is { IsGraduated: false } && m.ReviewDueAt <= now)
                .Select(m => m!.ReviewDueAt!.Value)
                .DefaultIfEmpty(DateTimeOffset.MaxValue)
                .Min()))
            .Where(x => x.Due != DateTimeOffset.MaxValue)
            .OrderBy(x => x.Due)
            .Select(x => x.Concept);

    private static DeckQuestion? Lowest(IEnumerable<DeckQuestion> candidates, Random rng) =>
        candidates.OrderBy(q => q.Difficulty).ThenBy(_ => rng.Next()).FirstOrDefault();

    private static List<Guid> Shuffle(List<Guid> items, Random rng)
    {
        var copy = items.ToArray();
        rng.Shuffle(copy);
        return [.. copy];
    }

    private sealed class Draft(IReadOnlyList<DeckQuestion> pool, IReadOnlySet<Guid> seen, (int Min, int Max) band, Random rng, bool enforceMinimums)
    {
        public List<DeckSlot> Slots { get; } = [];
        private readonly HashSet<Guid> _concepts = [];
        private readonly HashSet<Guid> _questions = [];

        public void Add(DeckQuestion question, MatchSlot slot)
        {
            Slots.Add(new DeckSlot(question, slot));
            _concepts.Add(question.ConceptId);
            _questions.Add(question.Id);
        }

        public void AddUpTo(IEnumerable<Guid> concepts, MatchSlot slot, int max)
        {
            var added = 0;
            foreach (var concept in concepts)
            {
                if (added >= max || Slots.Count >= GameRules.QuestionsPerMatch) return;
                if (_concepts.Contains(concept)) continue;
                var question = Best(pool.Where(q => q.ConceptId == concept), allowSeen: false);
                if (question is null) continue;
                Add(question, slot);
                added++;
            }
        }

        // Swaps core questions for missing required types (≥1 find the bug, ≥1 output, ≥1 drag-and-drop).
        public void RepairMinimums(bool allowSeen)
        {
            foreach (var needed in MissingTypes())
            {
                foreach (var slot in Slots.Where(s => s.Slot == MatchSlot.Core && !IsLastOfRequired(s.Question.Type)).ToList())
                {
                    var replacement = Best(
                        pool.Where(q => needed.Contains(q.Type) && (q.ConceptId == slot.Question.ConceptId || !_concepts.Contains(q.ConceptId))),
                        allowSeen);
                    if (replacement is null) continue;
                    Slots[Slots.IndexOf(slot)] = new DeckSlot(replacement, MatchSlot.Core);
                    _concepts.Remove(slot.Question.ConceptId);
                    _concepts.Add(replacement.ConceptId);
                    _questions.Add(replacement.Id);
                    break;
                }
            }
        }

        // Thin pool: allow questions seen in the last 30 days, then concepts that are already in the deck.
        public void Degrade(IReadOnlyList<Guid> concepts)
        {
            foreach (var concept in Shuffle([.. concepts], rng))
            {
                if (Slots.Count >= GameRules.QuestionsPerMatch) return;
                if (_concepts.Contains(concept)) continue;
                if (Best(pool.Where(q => q.ConceptId == concept), allowSeen: true) is { } question) Add(question, MatchSlot.Core);
            }
            while (Slots.Count < GameRules.QuestionsPerMatch && Best(pool, allowSeen: true) is { } extra) Add(extra, MatchSlot.Core);
        }

        // In the band first, then the nearest difficulty; types still required by the minimums win ties within a band step.
        private DeckQuestion? Best(IEnumerable<DeckQuestion> candidates, bool allowSeen) =>
            candidates
                .Where(q => !_questions.Contains(q.Id) && Fits(q.Type) && (allowSeen || !seen.Contains(q.Id)))
                .OrderBy(q => BandDistance(q.Difficulty) * 2 + (IsRequired(q.Type) ? 0 : 1))
                .ThenBy(_ => rng.Next())
                .FirstOrDefault();

        private int Count(QuestionType type) => Slots.Count(s => s.Question.Type == type);

        private int DragAndDropCount => Slots.Count(s => GameRules.DragAndDrop.Contains(s.Question.Type));

        private bool Fits(QuestionType type) =>
            Count(type) < GameRules.Types[type].MaxPerDeck
            && (!GameRules.DragAndDrop.Contains(type) || DragAndDropCount < GameRules.DragAndDropMax);

        private bool IsRequired(QuestionType type) =>
            enforceMinimums && (Count(type) < GameRules.Types[type].MinPerDeck
                || (GameRules.DragAndDrop.Contains(type) && DragAndDropCount < GameRules.DragAndDropMin));

        private bool IsLastOfRequired(QuestionType type) =>
            (GameRules.Types[type].MinPerDeck > 0 && Count(type) <= GameRules.Types[type].MinPerDeck)
            || (GameRules.DragAndDrop.Contains(type) && DragAndDropCount <= GameRules.DragAndDropMin);

        private IEnumerable<QuestionType[]> MissingTypes()
        {
            foreach (var (type, rule) in GameRules.Types)
                if (Count(type) < rule.MinPerDeck) yield return [type];
            if (DragAndDropCount < GameRules.DragAndDropMin) yield return GameRules.DragAndDrop;
        }

        private int BandDistance(int difficulty) =>
            difficulty < band.Min ? band.Min - difficulty : difficulty > band.Max ? difficulty - band.Max : 0;
    }
}
