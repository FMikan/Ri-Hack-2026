using System.Text.Json;
using System.Text.Json.Nodes;
using App.Domain;
using App.Domain.Content;

namespace App.Application.Content;

// The only place outside a running match that turns a stored question into what the client sees.
public static class QuestionView
{
    public static QuestionPublicDto Public(Question q) =>
        new(q.Id, q.Type, q.Difficulty, q.Language, GameRules.Types[q.Type].TimeMs, GameRules.Types[q.Type].BasePoints, QuestionJson.Public(q.Type, q.Content));

    public static AnswerKeyDto Key(Question q) => new(JsonNode.Parse(q.Content)!["answer"]!.DeepClone(), q.Remember);

    // The text a list shows for a question: its prompt, or the statement of a true/false question.
    public static string Prompt(string content)
    {
        var node = JsonNode.Parse(content)!;
        return (node["prompt"] ?? node["statement"])?.GetValue<string>() ?? "";
    }

    public static ExplanationDto? Explanation(string? cached) =>
        cached is null ? null : JsonSerializer.Deserialize<GeneratedExplanation>(cached, JsonSerializerOptions.Web) is { } e
            ? new ExplanationDto(e.WhyCorrect, e.WhyDistractor, e.Remember, "ai")
            : null;
}
