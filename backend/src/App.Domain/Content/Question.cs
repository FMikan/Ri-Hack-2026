using System.Text.Json;
using System.Text.Json.Nodes;

namespace App.Domain.Content;

public enum QuestionType { TrueFalse, MultipleChoice, Cloze, OddOneOut, Output, MatchPairs, Order, FindBug }

public enum QuestionStatus { Active, Reported, Quarantine, Retired }

public class Question
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public string? SeedKey { get; set; } // 'sql.n-plus-one.mc.001', makes seeding idempotent
    public Guid TopicId { get; set; }
    public Guid ConceptId { get; set; }
    public QuestionType Type { get; set; }
    public int Difficulty { get; set; } // 1..4 (D1–D4)
    public string? Language { get; set; } // language of the code, if any
    public required string Content { get; set; } // jsonb, shape per type (docs §3.2.1), includes "answer"
    public required string Remember { get; set; } // one "remember this" sentence; static fallback explanation
    public string? Explanation { get; set; } // jsonb cache of the AI explanation (F2)
    public QuestionStatus Status { get; set; } = QuestionStatus.Active;
    public required DateTimeOffset CreatedAt { get; init; }
}

// One player's report of a broken question; the third report quarantines the question (docs §3.2).
public class QuestionReport
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required Guid QuestionId { get; init; }
    public required Guid UserId { get; init; }
    public required string Reason { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

// questions.content holds the public part and the key ("answer") together; these split them.
public static class QuestionJson
{
    public static JsonObject Public(QuestionType type, string content)
    {
        var node = JsonNode.Parse(content)!.AsObject();
        node.Remove("answer");
        node.Insert(0, "type", JsonNamingPolicy.SnakeCaseLower.ConvertName(type.ToString()));
        return node;
    }

    public static JsonElement Answer(string content)
    {
        using var document = JsonDocument.Parse(content);
        return document.RootElement.GetProperty("answer").Clone();
    }
}

// Auto-calibration (idea §3): after 50 answers at its current difficulty, a question more than 85 % of players get right
// moves a band down, one fewer than 30 % get right a band up (D1–D4).
public static class QuestionCalibration
{
    public static int Next(int difficulty, int answers, double correctRatio)
    {
        if (answers < GameRules.CalibrationMinAnswers) return difficulty;
        var next = correctRatio > GameRules.CalibrationEasyRatio ? difficulty - 1 : correctRatio < GameRules.CalibrationHardRatio ? difficulty + 1 : difficulty;
        return Math.Clamp(next, 1, 4);
    }
}
