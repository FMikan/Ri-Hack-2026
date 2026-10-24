using App.Domain.Content;

namespace App.Application.Content;

// Content is the full stored question, key included: the generator explains why the key is right.
public record ExplanationInput(string Topic, string Concept, QuestionType Type, string? Language, string Content, string Remember);

public record GeneratedExplanation(string WhyCorrect, string WhyDistractor, string Remember);

// Live AI explanation of one question (idea §16), cached on the question after the first success.
public interface IExplanationGenerator
{
    Task<GeneratedExplanation> ExplainAsync(ExplanationInput input, CancellationToken cancellationToken);
}
