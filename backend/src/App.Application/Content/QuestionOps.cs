using App.Domain;
using App.Domain.Content;
using Microsoft.EntityFrameworkCore;

namespace App.Application.Content;

public static class QuestionOps
{
    // Idea §17: a retired question leaves every future deck. Finished matches keep their results; the mastery each answer
    // to it moved is taken back (clamped to 0–100), and statistics skip those answers from now on.
    public static async Task RetireQuestionAsync(this IAppDbContext db, Guid questionId, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlAsync(
            $"""
            update concept_mastery m set mastery = greatest(0, least(100, m.mastery - d.delta))
            from (select user_id, concept_id, sum(mastery_after - mastery_before) as delta
                  from answers where question_id = {questionId} and reverted_at is null
                  group by user_id, concept_id) d
            where m.user_id = d.user_id and m.concept_id = d.concept_id
            """, ct);
        await db.Answers.Where(a => a.QuestionId == questionId && a.RevertedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.RevertedAt, now), ct);
        await db.Questions.Where(q => q.Id == questionId)
            .ExecuteUpdateAsync(s => s.SetProperty(q => q.Status, QuestionStatus.Retired), ct);
        await transaction.CommitAsync(ct);
    }

    // Once a day (QuestionCalibration): only answers given at the question's current difficulty count, so a question
    // that moved needs 50 fresh answers before it can move again.
    public static async Task RecalibrateDifficultyAsync(this IAppDbContext db, CancellationToken ct)
    {
        var stats = await db.Answers.Where(a => a.RevertedAt == null)
            .Join(db.Questions.Where(q => q.Status == QuestionStatus.Active), a => a.QuestionId, q => q.Id, (a, q) => new { a.IsCorrect, a.Difficulty, Current = q.Difficulty, q.Id })
            .Where(x => x.Difficulty == x.Current)
            .GroupBy(x => new { x.Id, x.Current })
            .Where(g => g.Count() >= GameRules.CalibrationMinAnswers)
            .Select(g => new { g.Key.Id, g.Key.Current, Answers = g.Count(), Ratio = g.Average(x => x.IsCorrect ? 1.0 : 0.0) })
            .ToListAsync(ct);
        foreach (var question in stats)
        {
            var next = QuestionCalibration.Next(question.Current, question.Answers, question.Ratio);
            if (next != question.Current)
                await db.Questions.Where(q => q.Id == question.Id).ExecuteUpdateAsync(s => s.SetProperty(q => q.Difficulty, next), ct);
        }
    }
}
