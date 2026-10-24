using System.Text.Json;
using App.Domain;
using App.Domain.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace App.Application.Content;

public class ContentService(IAppDbContext db, IExplanationGenerator explainer, AiOptions ai, ILogger<ContentService> logger, TimeProvider time)
{
    // Only topics with enough questions to build a full deck are offered (the seeder sets is_active).
    public Task<List<TopicDto>> ListTopicsAsync(CancellationToken cancellationToken) =>
        db.Topics
            .Where(t => t.IsActive)
            .OrderBy(t => t.SortOrder)
            .Select(t => new TopicDto(t.Id, t.Slug, t.Name, t.Description, t.Concepts.Count))
            .ToListAsync(cancellationToken);

    public async Task<List<ConceptDto>> ListConceptsAsync(string topicSlug, CancellationToken cancellationToken)
    {
        var topic = await db.Topics.Include(t => t.Concepts).FirstOrDefaultAsync(t => t.Slug == topicSlug, cancellationToken)
            ?? throw new AppException(ErrorCodes.TopicNotFound, $"Topic '{topicSlug}' does not exist.", 404);
        return topic.Concepts.OrderBy(c => c.SortOrder).Select(c => new ConceptDto(c.Id, c.Slug, c.Name, c.TopicId)).ToList();
    }

    public async Task<QuestionDetailDto> GetQuestionAsync(Guid userId, Guid questionId, CancellationToken cancellationToken)
    {
        var question = await db.Questions.AsNoTracking().FirstOrDefaultAsync(q => q.Id == questionId, cancellationToken) ?? throw QuestionNotFound();
        var answered = await db.Answers.AnyAsync(a => a.UserId == userId && a.QuestionId == questionId, cancellationToken);
        var shown = QuestionView.Public(question);
        return new QuestionDetailDto(shown.Id, shown.Type, shown.Difficulty, shown.Language, shown.TimeLimitMs, shown.BasePoints, shown.Content,
            answered ? QuestionView.Key(question) : null);
    }

    // Generated on the first request and cached on the question for everyone; never called while a match polls.
    public async Task<ExplanationDto> ExplainAsync(Guid userId, Guid questionId, CancellationToken cancellationToken)
    {
        var question = await AnsweredAsync(userId, questionId, cancellationToken);
        if (QuestionView.Explanation(question.Explanation) is { } cached) return cached;

        var names = await db.Concepts.Where(c => c.Id == question.ConceptId)
            .Join(db.Topics, c => c.TopicId, t => t.Id, (c, t) => new { Concept = c.Name, Topic = t.Name })
            .FirstAsync(cancellationToken);
        var input = new ExplanationInput(names.Topic, names.Concept, question.Type, question.Language, question.Content, question.Remember);
        var generated = await ai.TryAsync(token => explainer.ExplainAsync(input, token), logger, cancellationToken);
        if (generated is null) return new ExplanationDto("", "", question.Remember, "static");

        // A concurrent first request may have cached its own explanation already; the first one stays.
        var json = JsonSerializer.Serialize(generated, JsonSerializerOptions.Web);
        await db.Questions.Where(q => q.Id == questionId && q.Explanation == null)
            .ExecuteUpdateAsync(s => s.SetProperty(q => q.Explanation, json), cancellationToken);
        return new ExplanationDto(generated.WhyCorrect, generated.WhyDistractor, generated.Remember, "ai");
    }

    // One report per player and question; the third player's report takes the question out of new decks.
    // Finished matches keep it, so their results and reviews never change.
    public async Task ReportAsync(Guid userId, Guid questionId, string reason, CancellationToken cancellationToken)
    {
        await AnsweredAsync(userId, questionId, cancellationToken);
        var now = time.GetUtcNow();
        await db.Database.ExecuteSqlAsync(
            $"insert into question_reports (id, question_id, user_id, reason, created_at) values ({Guid.CreateVersion7()}, {questionId}, {userId}, {reason.Trim()}, {now}) on conflict (question_id, user_id) do nothing",
            cancellationToken);
        if (await db.QuestionReports.CountAsync(r => r.QuestionId == questionId, cancellationToken) >= GameRules.ReportsToQuarantine)
            await db.Questions.Where(q => q.Id == questionId && q.Status == QuestionStatus.Active)
                .ExecuteUpdateAsync(s => s.SetProperty(q => q.Status, QuestionStatus.Quarantine), cancellationToken);
    }

    private async Task<Question> AnsweredAsync(Guid userId, Guid questionId, CancellationToken cancellationToken)
    {
        var question = await db.Questions.AsNoTracking().FirstOrDefaultAsync(q => q.Id == questionId, cancellationToken) ?? throw QuestionNotFound();
        if (!await db.Answers.AnyAsync(a => a.UserId == userId && a.QuestionId == questionId, cancellationToken))
            throw new AppException(ErrorCodes.QuestionNotAnswered, "Answer this question first.", 403);
        return question;
    }

    private static AppException QuestionNotFound() => new(ErrorCodes.QuestionNotFound, "This question does not exist.", 404);
}
