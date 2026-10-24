using App.Domain.Progress;

namespace App.Application;

public static class LearningLog
{
    // Moves the concept's mastery (and review step) for one answer and appends the answer to the log (docs §3.3).
    // FindAsync also sees rows added earlier in the same unit of work, so two answers on one concept chain correctly.
    public static async Task RecordAsync(this IAppDbContext db, Answer answer, bool timed, CancellationToken cancellationToken)
    {
        var concept = await db.ConceptMastery.FindAsync([answer.UserId, answer.ConceptId], cancellationToken);
        if (concept is null)
        {
            concept = new ConceptMastery { UserId = answer.UserId, ConceptId = answer.ConceptId, TopicId = answer.TopicId };
            db.ConceptMastery.Add(concept);
        }
        answer.MasteryBefore = concept.Mastery;
        var weight = Mastery.Weight(answer.Difficulty, answer.QuestionType, answer.Fraction, answer.Label ?? AnswerLabel.Miss, timed);
        Mastery.Apply(concept, answer.Fraction, weight, answer.Difficulty, answer.CreatedAt);
        answer.MasteryAfter = concept.Mastery;
        db.Answers.Add(answer);
    }
}
