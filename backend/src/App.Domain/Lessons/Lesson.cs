using App.Domain.Matches;
using App.Domain.Progress;

namespace App.Domain.Lessons;

public enum LessonStatus { Generated, Passed, Failed }

public enum LessonCardsSource { Ai, Cache, Fallback }

// One card per lesson concept: the term, why it matters, an example, a common misconception (idea §10).
public record LessonCard(Guid ConceptId, string Title, string Why, string? Code, string? Language, string Misconception);

// A lesson after a match (docs §3.6): cards for the missed and chronically weak concepts, then three check questions.
public class Lesson
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserId { get; init; }
    public Guid? MatchId { get; init; }
    public LessonStatus Status { get; set; } = LessonStatus.Generated;
    public required List<Guid> ConceptIds { get; init; }
    public required string Cards { get; init; } // jsonb: LessonCard[]
    public required LessonCardsSource CardsSource { get; init; }
    public required List<Guid> CheckQuestionIds { get; set; } // [0] is the variant of the missed question
    public string? CheckResults { get; set; } // jsonb: the last attempt, [{questionId, payload, isCorrect}]
    public int Attempt { get; set; } = 1;
    public DateTimeOffset? LastAttemptAt { get; set; }
    public bool FirstForMatch { get; init; } = true; // a second lesson for the same match earns no reward
    public bool Rewarded { get; set; }
    public DateTimeOffset? RetryAfter { get; set; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? FinishedAt { get; set; }
}

// AI cards are cached per concept and difficulty band, so a concept is generated once per band for everyone.
public class ConceptLessonCards
{
    public required Guid ConceptId { get; init; }
    public required int Band { get; init; }
    public required string Cards { get; init; } // jsonb: LessonCard[]
    public required DateTimeOffset CreatedAt { get; init; }
}

public static class LessonRules
{
    // The missed concepts, costliest first (the variant's concept leads), then up to two weak concepts the player
    // has already answered and keeps missing ("chronic gaps").
    public static List<Guid> Concepts(IEnumerable<(Guid ConceptId, double Cost)> misses, IEnumerable<ConceptMastery> topicMastery, Guid? first = null)
    {
        var allMissed = misses
            .OrderByDescending(m => m.ConceptId == first)
            .ThenByDescending(m => m.Cost)
            .Select(m => m.ConceptId)
            .Distinct()
            .ToList();
        var missed = allMissed.Take(GameRules.LessonMissedConcepts).ToList();
        // A concept missed in this very match is not "chronic", even when it did not make the cut above.
        var chronic = topicMastery
            .Where(m => m.State == ConceptState.Weak && !allMissed.Contains(m.ConceptId))
            .OrderBy(m => m.Mastery)
            .Take(GameRules.LessonChronicConcepts)
            .Select(m => m.ConceptId);
        return [.. missed, .. chronic];
    }

    // Same concept, same difficulty, another type; then a neighbouring difficulty; then the same type. Unseen first.
    public static DeckQuestion? Variant(IReadOnlyList<DeckQuestion> pool, DeckQuestion missed, IReadOnlySet<Guid> seen, IReadOnlySet<Guid> exclude, Random rng)
    {
        var candidates = pool.Where(q => q.ConceptId == missed.ConceptId && q.Id != missed.Id && !exclude.Contains(q.Id)).ToList();
        Func<DeckQuestion, bool>[] steps =
        [
            q => q.Difficulty == missed.Difficulty && q.Type != missed.Type,
            q => Math.Abs(q.Difficulty - missed.Difficulty) == 1 && q.Type != missed.Type,
            q => q.Type == missed.Type,
            _ => true,
        ];
        foreach (var allowSeen in new[] { false, true })
            foreach (var step in steps)
                if (candidates
                        .Where(q => step(q) && (allowSeen || !seen.Contains(q.Id)))
                        .OrderBy(q => Math.Abs(q.Difficulty - missed.Difficulty))
                        .ThenBy(_ => rng.Next())
                        .FirstOrDefault() is { } variant)
                    return variant;
        return null;
    }

    // Two more checks from the lesson concepts: unseen first, then concepts not covered yet, then closest to the band.
    public static List<DeckQuestion> OtherChecks(
        IReadOnlyList<DeckQuestion> pool, IReadOnlyCollection<Guid> conceptIds, DeckQuestion? variant,
        IReadOnlySet<Guid> seen, IReadOnlySet<Guid> exclude, (int Min, int Max) band, Random rng)
    {
        var candidates = pool.Where(q => conceptIds.Contains(q.ConceptId) && q.Id != variant?.Id && !exclude.Contains(q.Id)).ToList();
        var picked = new List<DeckQuestion>();
        while (picked.Count < GameRules.LessonOtherChecks
               && candidates
                   .Where(q => !picked.Contains(q))
                   .OrderBy(q => seen.Contains(q.Id))
                   .ThenBy(q => q.ConceptId == variant?.ConceptId || picked.Any(p => p.ConceptId == q.ConceptId))
                   .ThenBy(q => Math.Max(0, Math.Max(band.Min - q.Difficulty, q.Difficulty - band.Max)))
                   .ThenBy(_ => rng.Next())
                   .FirstOrDefault() is { } next)
            picked.Add(next);
        return picked;
    }

    // The variant is the gap the lesson patches, so it must be right, plus at least one of the other two (idea §10).
    public static bool Passed(bool variantCorrect, int othersCorrect) => variantCorrect && othersCorrect >= 1;

    // Not yet rewarded, the first lesson for its match, and no earlier attempt today (a failed try waits for tomorrow).
    public static bool RewardEligible(Lesson lesson, DateTimeOffset now) =>
        lesson.Status != LessonStatus.Passed && !lesson.Rewarded && lesson.FirstForMatch
        && (lesson.LastAttemptAt is not { } last || Clock.Today(last) != Clock.Today(now));
}
