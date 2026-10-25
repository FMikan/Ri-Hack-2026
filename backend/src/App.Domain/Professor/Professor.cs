using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using App.Domain.Content;

namespace App.Domain.Professor;

public enum ProfessorRole { User, Professor }

// What an answer is about: a concrete question (a quiz checks it) or a broader subject (a lesson teaches it).
public enum ProfessorScope { Specific, General }

// A chat with the AI Professor (docs §3.13). Only finished turns are stored: a question and its answer are saved together.
public class ProfessorChat
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserId { get; init; }
    public required string Title { get; set; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; set; }
}

public class ProfessorMessage
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid ChatId { get; init; }
    public required ProfessorRole Role { get; init; }
    public required string Text { get; init; }
    public string? Topic { get; init; } // a professor answer about IT: what a lesson or quiz made from it covers
    public ProfessorScope? Scope { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

public record ProfessorCard(string Title, string Body, string? Code, string? Language, string? Misconception);

// A lesson written from one professor answer (one per answer). For learning only: no rewards, no mastery.
public class ProfessorLesson
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserId { get; init; }
    public required Guid ChatId { get; init; }
    public required Guid MessageId { get; init; }
    public required string Topic { get; init; }
    public required string Title { get; init; }
    public required string Summary { get; init; }
    public required string Cards { get; init; } // jsonb: ProfessorCard[]
    public required DateTimeOffset CreatedAt { get; init; }
}

// Content has the shape of questions.content, key in "answer" (docs §3.2.1), so QuestionJson and Grader work on it.
public record ProfessorQuizQuestion(QuestionType Type, string? Language, JsonElement Content, string Explanation);

public record ProfessorQuizAnswer(int Index, JsonElement Payload, bool IsCorrect);

// A practice quiz written from one professor answer (one per answer). Graded from its key, never scored.
public class ProfessorQuiz
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid UserId { get; init; }
    public required Guid ChatId { get; init; }
    public required Guid MessageId { get; init; }
    public required string Topic { get; init; }
    public required string Title { get; init; }
    public required string Questions { get; init; } // jsonb: ProfessorQuizQuestion[]
    public string Answers { get; set; } = "[]"; // jsonb: ProfessorQuizAnswer[], at most one per index
    public required DateTimeOffset CreatedAt { get; init; }
}

public static class ProfessorRules
{
    public const int MessageMaxLength = 2000, HistoryMessages = 20, TitleMaxLength = 60, TopicMaxLength = 80;
    public const int QuizQuestions = 5, QuizMinQuestions = 3, LessonMinCards = 2, LessonMaxCards = 6;
    public static readonly TimeSpan ChatTimeout = TimeSpan.FromMinutes(2), GenerationTimeout = TimeSpan.FromSeconds(90);

    // The jsonb columns store question types as "multiple_choice", the spelling the API and questions.type use.
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private const string HeaderPrefix = "TOPIC:";

    // Every answer starts with a header line the player never sees: "TOPIC: specific | JWT expiry",
    // "TOPIC: general | Docker networking" or "TOPIC: none" (not about IT). False when the line is not a header.
    public static bool TryParseHeader(string line, out string? topic, out ProfessorScope? scope)
    {
        (topic, scope) = (null, null);
        var text = line.Replace("*", "").Replace("`", "").Trim().TrimStart('#').Trim();
        if (!text.StartsWith(HeaderPrefix, StringComparison.OrdinalIgnoreCase)) return false;

        var rest = text[HeaderPrefix.Length..].Trim();
        var bar = rest.IndexOf('|');
        var name = (bar >= 0 ? rest[(bar + 1)..] : rest).Trim().Trim('"', '\'', '.').Trim();
        if (name.Length == 0 || name.Equals("none", StringComparison.OrdinalIgnoreCase)) return true;

        scope = bar >= 0 && rest[..bar].Trim().Equals("general", StringComparison.OrdinalIgnoreCase) ? ProfessorScope.General : ProfessorScope.Specific;
        topic = Truncate(name, TopicMaxLength);
        return true;
    }

    // The header as the model wrote it, put back in front of stored answers when the history goes to the model again,
    // so it keeps following the format.
    public static string Header(string? topic, ProfessorScope? scope) =>
        topic is null ? $"{HeaderPrefix} none" : $"{HeaderPrefix} {(scope == ProfessorScope.General ? "general" : "specific")} | {topic}";

    public static string Title(string? topic, string question) => Truncate(topic ?? question.ReplaceLineEndings(" ").Trim(), TitleMaxLength);

    // One generated question in the stored shape with its options shuffled (models favour the first slots for the key).
    // Null when it is malformed; the quiz drops it.
    public static ProfessorQuizQuestion? QuizQuestion(
        string type, string prompt, string? code, string? language, IReadOnlyList<string> options, string correct, string explanation, Random rng)
    {
        prompt = prompt.Trim();
        explanation = explanation.Trim();
        code = Code(code);
        if (prompt.Length == 0 || explanation.Length == 0) return null;
        var key = correct.Trim().ToLowerInvariant();

        if (type == "true_false")
        {
            // The true/false view shows only the statement, so a statement about code cannot be shown.
            if (code is not null || key is not ("true" or "false")) return null;
            var statement = new JsonObject { ["statement"] = prompt, ["answer"] = new JsonObject { ["value"] = key == "true" } };
            return new ProfessorQuizQuestion(QuestionType.TrueFalse, null, JsonSerializer.SerializeToElement(statement), explanation);
        }

        var texts = options.Select(o => o.Trim()).ToList();
        var correctIndex = key.Length == 1 ? key[0] - 'a' : -1;
        if (type != "multiple_choice" || texts.Count != 4 || texts.Any(t => t.Length == 0)
            || texts.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 4 || correctIndex is < 0 or > 3)
            return null;

        var order = Enumerable.Range(0, 4).OrderBy(_ => rng.Next()).ToList();
        var content = new JsonObject { ["prompt"] = prompt };
        if (code is not null) content["code"] = code;
        content["options"] = new JsonArray(order.Select((from, slot) => (JsonNode)new JsonObject { ["id"] = OptionId(slot), ["text"] = texts[from] }).ToArray());
        content["answer"] = new JsonObject { ["optionId"] = OptionId(order.IndexOf(correctIndex)) };
        var lang = code is null || string.IsNullOrWhiteSpace(language) ? null : language.Trim().ToLowerInvariant();
        return new ProfessorQuizQuestion(QuestionType.MultipleChoice, lang, JsonSerializer.SerializeToElement(content), explanation);
    }

    // Generated code without a surrounding ``` fence; null when there is none.
    public static string? Code(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var lines = code.ReplaceLineEndings("\n").Trim('\n').Split('\n').ToList();
        if (lines[0].TrimStart().StartsWith("```")) lines.RemoveAt(0);
        if (lines.Count > 0 && lines[^1].Trim() == "```") lines.RemoveAt(lines.Count - 1);
        var text = string.Join('\n', lines).TrimEnd();
        return text.Trim().Length == 0 ? null : text;
    }

    private static string OptionId(int slot) => ((char)('a' + slot)).ToString();

    // Never splits a surrogate pair (an emoji): half of one is not valid UTF-8, and PostgreSQL would refuse the row.
    private static string Truncate(string text, int max)
    {
        if (text.Length <= max) return text;
        var cut = char.IsHighSurrogate(text[max - 2]) ? max - 2 : max - 1;
        return text[..cut].TrimEnd() + "…";
    }
}

// Takes the header line off a streamed answer, so the player sees only the answer, and keeps what it said.
public class ProfessorHeaderReader
{
    private const int MaxHeaderLength = 200; // a first line this long is answer text, not a header

    private readonly StringBuilder _pending = new();
    private bool _headerRead, _leading = true;

    public string? Topic { get; private set; }
    public ProfessorScope? Scope { get; private set; }

    // The answer text in this chunk; empty while the header is still arriving.
    public string Push(string chunk)
    {
        if (_headerRead) return Body(chunk);
        _pending.Append(chunk);
        var text = _pending.ToString().TrimStart();
        var newline = text.IndexOf('\n');
        if (newline < 0 && text.Length < MaxHeaderLength) return "";
        return Split(text, newline);
    }

    // What is left when the stream ends (an answer shorter than one line).
    public string Finish() => _headerRead ? "" : Split(_pending.ToString().TrimStart(), -1);

    private string Split(string text, int newline)
    {
        _headerRead = true;
        _pending.Clear();
        var first = newline < 0 ? text : text[..newline];
        if (!ProfessorRules.TryParseHeader(first, out var topic, out var scope)) return Body(text);
        (Topic, Scope) = (topic, scope);
        return newline < 0 ? "" : Body(text[(newline + 1)..]);
    }

    // Drops the blank line between the header and the answer.
    private string Body(string text)
    {
        if (!_leading) return text;
        text = text.TrimStart('\r', '\n');
        if (text.Length > 0) _leading = false;
        return text;
    }
}
