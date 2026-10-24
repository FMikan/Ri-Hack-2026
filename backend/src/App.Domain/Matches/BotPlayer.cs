using System.Text.Json;
using System.Text.Json.Nodes;
using App.Domain.Content;

namespace App.Domain.Matches;

public enum BotPersona { VibeCoder, Junior, Mid, Senior }

public record BotProfile(int DisplayRating, double Accuracy, int MinMs, int MaxMs);

// The bot knows nothing: it has the key and simulates accuracy and speed (idea §12).
public static class BotPlayer
{
    public static readonly IReadOnlyDictionary<BotPersona, BotProfile> Personas = new Dictionary<BotPersona, BotProfile>
    {
        [BotPersona.VibeCoder] = new(1000, 0.50, 3_000, 6_000),
        [BotPersona.Junior] = new(1200, 0.60, 8_000, 12_000),
        [BotPersona.Mid] = new(1500, 0.75, 6_000, 9_000),
        [BotPersona.Senior] = new(1800, 0.88, 4_000, 7_000),
    };

    public static double AccuracyFor(BotPersona persona, int difficulty) => Personas[persona].Accuracy - 0.08 * (difficulty - 2);

    public static BotPersona Recommended(int rating) =>
        Personas.MinBy(p => Math.Abs(p.Value.DisplayRating - rating)).Key;

    public static (JsonNode Payload, int ElapsedMs) Answer(QuestionType type, string content, int difficulty, BotPersona persona, int limitMs, Random rng)
    {
        var profile = Personas[persona];
        var elapsed = Math.Min(rng.Next(profile.MinMs, profile.MaxMs + 1), limitMs - GameRules.BotMinSlackMs);
        var question = JsonNode.Parse(content)!.AsObject();
        var answer = question["answer"]!;
        var correct = rng.NextDouble() < AccuracyFor(persona, difficulty);
        return (correct ? answer.DeepClone() : Wrong(type, question, answer, rng), elapsed);
    }

    // A wrong answer worth zero points, so partial-credit types do not hand the bot a free 25 %.
    private static JsonNode Wrong(QuestionType type, JsonObject question, JsonNode answer, Random rng)
    {
        switch (type)
        {
            case QuestionType.TrueFalse:
                return new JsonObject { ["value"] = !answer["value"]!.GetValue<bool>() };
            case QuestionType.MultipleChoice or QuestionType.Cloze or QuestionType.Output:
                return new JsonObject { ["optionId"] = OtherId(question["options"]!, answer["optionId"]!.GetValue<string>(), rng) };
            case QuestionType.OddOneOut:
                return new JsonObject { ["itemId"] = OtherId(question["items"]!, answer["itemId"]!.GetValue<string>(), rng) };
            case QuestionType.MatchPairs:
            {
                // Shift every right item to the next left item: no pair stays correct.
                var pairs = answer["pairs"]!.AsArray();
                var shifted = pairs.Select((p, i) => new JsonObject
                {
                    ["leftId"] = p!["leftId"]!.GetValue<string>(),
                    ["rightId"] = pairs[(i + 1) % pairs.Count]!["rightId"]!.GetValue<string>(),
                });
                return new JsonObject { ["pairs"] = new JsonArray([.. shifted]) };
            }
            case QuestionType.Order:
            {
                var ids = answer["orderedIds"]!.AsArray().Select(n => n!.GetValue<string>()).ToList();
                return new JsonObject { ["orderedIds"] = new JsonArray([.. ids.Skip(1).Append(ids[0]).Select(id => JsonValue.Create(id))]) };
            }
            case QuestionType.FindBug:
            {
                var lines = question["code"]!.GetValue<string>().Split('\n').Length;
                var bugLines = Grader.BugLines(JsonDocument.Parse(answer.ToJsonString()).RootElement).ToHashSet();
                var wrongLines = Enumerable.Range(1, lines).Where(l => !bugLines.Contains(l)).ToList();
                var line = wrongLines[rng.Next(wrongLines.Count)];
                var fixes = question["fixes"]!.AsArray();
                return new JsonObject { ["line"] = line, ["fixId"] = fixes[rng.Next(fixes.Count)]!["id"]!.GetValue<string>() };
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    private static string OtherId(JsonNode options, string correctId, Random rng)
    {
        var others = options.AsArray().Select(o => o!["id"]!.GetValue<string>()).Where(id => id != correctId).ToList();
        return others[rng.Next(others.Count)];
    }
}
