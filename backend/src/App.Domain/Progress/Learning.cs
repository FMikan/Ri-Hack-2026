using App.Domain.Content;

namespace App.Domain.Progress;

public enum AnswerLabel { Brilliant, Good, Miss, Blunder }

public enum AnswerSource { Ranked, Ghost, Bot, Friend, Practice, Calibration, LessonCheck, DailyChallenge }

public enum ConceptState { Unknown, Weak, InProgress, Solid, Graduated }

// Mastery of one concept for one user (docs §3.3). Review columns are used from F2 (lessons and spaced repetition).
public class ConceptMastery
{
    public required Guid UserId { get; init; }
    public required Guid ConceptId { get; init; }
    public required Guid TopicId { get; init; }
    public double Mastery { get; set; } = GameRules.MasteryStart;
    public int AnswerCount { get; set; }
    public bool IsGraduated { get; set; }
    public int? ReviewStep { get; set; }
    public DateTimeOffset? ReviewDueAt { get; set; }
    public int ConsecutiveReviewSuccesses { get; set; }
    public DateTimeOffset? PatchedAt { get; set; }
    public DateTimeOffset? LastAnsweredAt { get; set; }

    public ConceptState State => ConceptStates.Of(this);
}

// Append-only log of every answer (docs §3.3): the source for statistics and for "seen in the last 30 days".
public class Answer
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserId { get; init; }
    public required Guid QuestionId { get; init; }
    public required Guid ConceptId { get; init; }
    public required Guid TopicId { get; init; }
    public required QuestionType QuestionType { get; init; }
    public required int Difficulty { get; init; }
    public required AnswerSource Source { get; init; }
    public Guid? MatchId { get; init; }
    public string? Payload { get; init; } // jsonb; null = timed out
    public required bool IsCorrect { get; init; }
    public required double Fraction { get; init; }
    public required int Points { get; init; }
    public required int ElapsedMs { get; init; }
    public int? TimeLimitMs { get; init; } // null = untimed practice
    public AnswerLabel? Label { get; init; }
    public required double MasteryBefore { get; init; }
    public required double MasteryAfter { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

public static class ConceptStates
{
    public static ConceptState Of(ConceptMastery? m) => m switch
    {
        null or { AnswerCount: 0 } => ConceptState.Unknown,
        { IsGraduated: true } => ConceptState.Graduated,
        { Mastery: < GameRules.WeakMastery } => ConceptState.Weak,
        { Mastery: < GameRules.SolidMastery } => ConceptState.InProgress,
        _ => ConceptState.Solid,
    };
}

public static class Scoring
{
    // base · fraction · (0.5 + 0.5 · remaining/total) for timed questions; untimed practice scores base · fraction.
    public static int Points(int basePoints, double fraction, int elapsedMs, int limitMs, bool timed)
    {
        if (fraction <= 0) return 0;
        var factor = timed ? 0.5 + 0.5 * Math.Clamp(limitMs - elapsedMs, 0, limitMs) / limitMs : 1;
        return (int)Math.Round(basePoints * fraction * factor, MidpointRounding.AwayFromZero);
    }
}

public static class Labels
{
    // Idea §10 labels; partial answers are never Brilliant or Blunder.
    public static AnswerLabel Of(int difficulty, double fraction, int elapsedMs, int limitMs, bool timedOut, bool timed)
    {
        if (timedOut) return AnswerLabel.Miss;
        var fast = timed && elapsedMs < 0.4 * limitMs;
        if (fraction >= 1) return fast && difficulty >= 3 ? AnswerLabel.Brilliant : AnswerLabel.Good;
        if (fraction >= 0.5) return AnswerLabel.Good;
        if (fraction > 0 || !timed) return AnswerLabel.Miss;
        return fast || difficulty == 1 ? AnswerLabel.Blunder : AnswerLabel.Miss;
    }
}

public static class Mastery
{
    // m' = m + α(r − m)w, α = max(0.15, 1/(n+1)), r = 100 · fraction (idea §8).
    public static double Next(double mastery, int answerCount, double r, double weight) =>
        Math.Clamp(mastery + Math.Max(GameRules.MasteryAlphaMin, 1.0 / (answerCount + 1)) * (r - mastery) * weight, 0, 100);

    // Difficulty × guessability (only when something was right) × 1.2 for a blunder × 0.5 untimed.
    public static double Weight(int difficulty, QuestionType type, double fraction, AnswerLabel label, bool timed)
    {
        var weight = GameRules.DifficultyWeight[difficulty];
        if (fraction > 0) weight *= GameRules.Types[type].Guessability;
        if (label == AnswerLabel.Blunder) weight *= GameRules.BlunderWeight;
        if (!timed) weight *= GameRules.UntimedWeight;
        return weight;
    }

    // ponytail: mastery only; review steps and graduation join in F2 with lessons.
    public static void Apply(ConceptMastery concept, double fraction, double weight, DateTimeOffset now)
    {
        concept.Mastery = Next(concept.Mastery, concept.AnswerCount, 100 * fraction, weight);
        concept.AnswerCount++;
        concept.LastAnsweredAt = now;
    }
}
