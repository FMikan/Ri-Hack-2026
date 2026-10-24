using System.Text.Json;
using Anthropic;
using Anthropic.Models.Beta.Messages;
using App.Application.Content;
using App.Application.Lessons;
using Microsoft.Extensions.Configuration;

namespace App.Infrastructure;

// Claude behind both AI interfaces (docs §5). Without ANTHROPIC_API_KEY every call throws NotSupportedException and the
// callers fall back (the question's "remember" sentence, the concept's description), so the game runs without AI.
// The caller bounds every call with Ai:TimeoutMs through the cancellation token.
public class ClaudeAi(IConfiguration configuration) : IExplanationGenerator, ILessonGenerator
{
    private const string Model = "claude-opus-5-5";

    private readonly AnthropicClient? _client =
        string.IsNullOrEmpty(configuration["ANTHROPIC_API_KEY"]) ? null : new AnthropicClient { ApiKey = configuration["ANTHROPIC_API_KEY"] };

    public Task<GeneratedExplanation> ExplainAsync(ExplanationInput input, CancellationToken cancellationToken) =>
        AskAsync<GeneratedExplanation>(
            """
            You explain answers in a quiz for software developers. Write in the language of the question.
            Fixed structure, at most 120 words in total:
            - whyCorrect: why the correct answer (in "answer") is correct.
            - whyDistractor: why the most tempting wrong option attracts players and why it is wrong.
            - remember: one sentence to remember.
            """,
            $"""
            Topic: {input.Topic}
            Concept: {input.Concept}
            Question type: {input.Type}{(input.Language is null ? "" : $", code language: {input.Language}")}
            Question with its key ("answer"): {input.Content}
            The author's one-line takeaway: {input.Remember}
            """,
            Schema(("whyCorrect", "string"), ("whyDistractor", "string"), ("remember", "string")),
            cancellationToken);

    public async Task<GeneratedCard> CardAsync(LessonCardInput input, CancellationToken cancellationToken)
    {
        var card = await AskAsync<GeneratedCard>(
            """
            You write one short lesson card for a developer who just missed questions on a concept in a quiz duel.
            Fields:
            - title: the term, a few words.
            - why: why it matters in practice, two or three sentences.
            - code: a minimal example (at most 12 lines, 60 characters per line), or "" when code does not help.
            - language: the code's language (js, ts, sql, py, cs, http, json, text), or "" without code.
            - misconception: one common misconception and what is true instead.
            Write in English. Match the depth to the band: 1 beginner, 2 junior, 3 mid, 4 senior.
            """,
            $"""
            Topic: {input.Topic}
            Concept: {input.Concept}: {input.ConceptDescription}
            Band: {input.Band}
            """,
            Schema(("title", "string"), ("why", "string"), ("code", "string"), ("language", "string"), ("misconception", "string")),
            cancellationToken);
        return card with { Code = NullIfEmpty(card.Code), Language = NullIfEmpty(card.Language) };
    }

    private async Task<T> AskAsync<T>(string system, string prompt, Dictionary<string, JsonElement> schema, CancellationToken cancellationToken)
    {
        if (_client is null) throw new NotSupportedException("ANTHROPIC_API_KEY is not set.");

        var response = await _client.Beta.Messages.Create(new MessageCreateParams
        {
            Model = Model,
            MaxTokens = 2048,
            // A request the safety classifiers decline is re-served by a fallback model in the same call.
            Betas = ["server-side-fallback-2026-07-01"],
            Fallbacks = new Default(),
            // Low effort: a short, well-specified answer that has to arrive within the 8 s budget.
            OutputConfig = new BetaOutputConfig { Effort = Effort.Low, Format = new BetaJsonOutputFormat { Schema = schema } },
            System = system,
            Messages = [new BetaUserMessageParam { Content = prompt }],
        }, cancellationToken);

        if (response.StopReason == "refusal") throw new InvalidOperationException("Claude declined the request.");
        var text = string.Concat(response.Content.Select(b => b.Value).OfType<BetaTextBlock>().Select(b => b.Text));
        return JsonSerializer.Deserialize<T>(text, JsonSerializerOptions.Web) ?? throw new InvalidOperationException("Claude returned no JSON.");
    }

    private static Dictionary<string, JsonElement> Schema(params (string Name, string Type)[] fields) => new()
    {
        ["type"] = JsonSerializer.SerializeToElement("object"),
        ["properties"] = JsonSerializer.SerializeToElement(fields.ToDictionary(f => f.Name, f => new { type = f.Type })),
        ["required"] = JsonSerializer.SerializeToElement(fields.Select(f => f.Name)),
        ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
    };

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
