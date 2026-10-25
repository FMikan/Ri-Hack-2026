using System.Runtime.CompilerServices;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Beta.Messages;
using App.Application.Content;
using App.Application.Lessons;
using App.Application.Professor;
using App.Domain;
using App.Domain.Professor;
using Microsoft.Extensions.Configuration;

namespace App.Infrastructure;

// Claude behind the AI interfaces (docs §5). Without ANTHROPIC_API_KEY every call throws NotSupportedException and the
// callers fall back (the question's "remember" sentence, the concept's description), so the game runs without AI; the
// professor reports itself unavailable instead. The caller bounds every call through the cancellation token
// (Ai:TimeoutMs for explanations and cards, ProfessorRules for the professor).
public class ClaudeAi(IConfiguration configuration) : IExplanationGenerator, ILessonGenerator, IProfessorAi
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

    public bool Available => _client is not null;

    // Effort low: a chat answer should start within a second or two. The header line comes first (ProfessorRules).
    public async IAsyncEnumerable<string> ChatAsync(IReadOnlyList<ProfessorTurnInput> history, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_client is null) throw new NotSupportedException("ANTHROPIC_API_KEY is not set.");

        var parameters = new MessageCreateParams
        {
            Model = Model,
            MaxTokens = 8192,
            Betas = ["server-side-fallback-2026-07-01"],
            Fallbacks = new Default(),
            OutputConfig = new BetaOutputConfig { Effort = Effort.Low },
            System = ProfessorSystem,
            // Plain text only: thinking blocks are never stored or replayed, so the history has nothing for the
            // conversation check on replayed thinking to reject.
            Messages = [.. history.Select(m => m.FromUser
                ? (BetaMessageParam)new BetaUserMessageParam { Content = m.Text }
                : new BetaAssistantMessageParam { Content = m.Text })],
        };
        var refused = false;
        await foreach (var streamEvent in _client.Beta.Messages.CreateStreaming(parameters, cancellationToken))
        {
            if (streamEvent.TryPickContentBlockDelta(out var block) && block.Delta.TryPickText(out var text))
                yield return text.Text;
            else if (streamEvent.TryPickDelta(out var delta) && delta.Delta.StopReason == "refusal")
                refused = true;
        }
        // The whole fallback chain declined. A mid-stream decline that a fallback model finished ends normally.
        if (refused) throw Declined();
    }

    public Task<GeneratedLesson> LessonAsync(StudyInput input, CancellationToken cancellationToken) =>
        AskAsync<GeneratedLesson>(
            """
            You write a short lesson for a developer who asked the AI Professor about a topic in a chat. They read it
            card by card in a quiz game for developers. Fields:
            - title: the lesson title, a few words.
            - summary: one or two sentences on what the lesson covers.
            - cards: 3 to 5 cards that build from the basics to the point of the chat. Each card:
              - title: the idea, a few words.
              - body: the explanation, 2 to 5 sentences of plain text (`inline code` is fine, no other formatting).
              - code: a minimal example (at most 12 lines, 60 characters per line, no ``` fence), or "" when code does not help.
              - language: the code's language (js, ts, py, cs, sql, bash, json, yaml, http, css, html, dockerfile, nginx), or "" without code.
              - misconception: one common mistake about this idea and what is true instead, or "".
            Write in the language of the player's question. Match the depth to the band: 1 beginner, 2 junior, 3 mid,
            4 senior. Be accurate; do not invent APIs, flags or versions.
            """,
            StudyPrompt(input),
            JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(LessonSchema)!,
            cancellationToken, Effort.Medium, 16000);

    public Task<GeneratedQuiz> QuizAsync(StudyInput input, CancellationToken cancellationToken) =>
        AskAsync<GeneratedQuiz>(
            """
            You write a short practice quiz for a developer who asked the AI Professor about a topic in a chat. It is
            for learning only: nothing is scored. Fields:
            - title: the quiz title, a few words.
            - questions: exactly 5 questions on the topic, from easier to harder, testing understanding rather than
              trivia. At least 3 are multiple_choice. Each question:
              - type: "multiple_choice" or "true_false".
              - prompt: the question; for true_false, a statement that is clearly true or clearly false.
              - code: for multiple_choice, a short snippet the question is about (at most 12 lines, 60 characters per
                line, no ``` fence), or "". A true_false question never has code.
              - language: the code's language (js, ts, py, cs, sql, bash, json, yaml, http, css, html, dockerfile, nginx), or "" without code.
              - options: for multiple_choice, exactly 4 different answers in the order a, b, c, d: one correct, three
                plausible mistakes. For true_false, [].
              - correct: for multiple_choice the letter of the correct option (a, b, c or d); for true_false "true" or "false".
              - explanation: why the correct answer is right and why the most tempting wrong one is wrong, at most 60 words.
            Write in the language of the player's question. Match the difficulty to the band: 1 beginner, 2 junior,
            3 mid, 4 senior. Every question must have exactly one correct answer: check each key before you answer.
            """,
            StudyPrompt(input),
            JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(QuizSchema)!,
            cancellationToken, Effort.Medium, 16000);

    private const string ProfessorSystem = """
        You are the AI Professor in StackOff, a quiz game for software developers. You teach IT and computer science:
        programming, web, networks, databases, security, operating systems, cloud and DevOps, algorithms and data
        structures, software design, and how AI works.

        Every reply starts with one header line, then an empty line, then your answer. The app hides the header, so
        never mention it. The header is one of:
        TOPIC: specific | <topic>   when the player asks a concrete question (how one thing works, why some code fails)
        TOPIC: general | <topic>    when the player wants to learn a broader subject ("teach me Docker")
        TOPIC: none                 when the message is not about IT or computer science, or is only small talk
        <topic> names what a lesson or quiz on this exchange would cover, in 2 to 6 words, in the player's language.

        Under every reply about IT the app shows two buttons, "Create lesson" and "Create quiz", that write a lesson or
        a practice quiz on the topic. Never write a lesson or quiz into the chat yourself.
        - Reply in the language the player writes in.
        - A specific question: answer it directly and clearly, then offer a quiz to check it, or a lesson, in one short sentence.
        - A general subject: give a short overview of the key ideas, then suggest a lesson to learn it step by step, or a quiz.
        - Not about IT or computer science: say briefly and kindly that you teach IT and computer science, and suggest
          a related question they could ask.
        - Be accurate. When you are not sure, say so; do not invent APIs, flags or versions.
        - Keep answers under about 250 words unless the player asks for more depth.

        Formatting: the app renders only paragraphs, "- " bullet lists, "1. " numbered lists, **bold**, `inline code`
        and fenced code blocks with a language tag (```js). No headings, tables, links, images or HTML.
        """;

    private static string StudyPrompt(StudyInput input) => $"""
        Topic: {input.Topic} ({(input.Scope == ProfessorScope.General ? "a broader subject" : "a concrete question")})
        Band: {input.Band}
        The player asked: {input.Question}
        The professor answered: {input.Answer}
        """;

    private const string LessonSchema = """
        {
          "type": "object",
          "properties": {
            "title": { "type": "string" },
            "summary": { "type": "string" },
            "cards": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "title": { "type": "string" },
                  "body": { "type": "string" },
                  "code": { "type": "string" },
                  "language": { "type": "string" },
                  "misconception": { "type": "string" }
                },
                "required": ["title", "body", "code", "language", "misconception"],
                "additionalProperties": false
              }
            }
          },
          "required": ["title", "summary", "cards"],
          "additionalProperties": false
        }
        """;

    private const string QuizSchema = """
        {
          "type": "object",
          "properties": {
            "title": { "type": "string" },
            "questions": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "type": { "type": "string", "enum": ["multiple_choice", "true_false"] },
                  "prompt": { "type": "string" },
                  "code": { "type": "string" },
                  "language": { "type": "string" },
                  "options": { "type": "array", "items": { "type": "string" } },
                  "correct": { "type": "string" },
                  "explanation": { "type": "string" }
                },
                "required": ["type", "prompt", "code", "language", "options", "correct", "explanation"],
                "additionalProperties": false
              }
            }
          },
          "required": ["title", "questions"],
          "additionalProperties": false
        }
        """;

    private static AppException Declined() =>
        new(ErrorCodes.ProfessorDeclined, "The AI Professor cannot help with that one. Try asking it another way.");

    // Low effort by default: a short, well-specified answer that has to arrive within the 8 s budget.
    private async Task<T> AskAsync<T>(
        string system, string prompt, Dictionary<string, JsonElement> schema, CancellationToken cancellationToken, Effort effort = Effort.Low, int maxTokens = 2048)
    {
        if (_client is null) throw new NotSupportedException("ANTHROPIC_API_KEY is not set.");

        var response = await _client.Beta.Messages.Create(new MessageCreateParams
        {
            Model = Model,
            MaxTokens = maxTokens,
            // A request the safety classifiers decline is re-served by a fallback model in the same call.
            Betas = ["server-side-fallback-2026-07-01"],
            Fallbacks = new Default(),
            OutputConfig = new BetaOutputConfig { Effort = effort, Format = new BetaJsonOutputFormat { Schema = schema } },
            System = system,
            Messages = [new BetaUserMessageParam { Content = prompt }],
        }, cancellationToken);

        if (response.StopReason == "refusal") throw Declined();
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
