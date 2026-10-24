using System.Text.Json;

namespace App.Domain.Content;

// Deterministic grading from the key (docs §7 of the idea): returns the fraction of the base points earned, 0..1.
// A malformed payload is simply wrong, never an exception.
public static class Grader
{
    public static double Grade(QuestionType type, JsonElement answer, JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object) return 0;
        try
        {
            return type switch
            {
                QuestionType.TrueFalse => Same(answer, payload, "value") ? 1 : 0,
                QuestionType.MultipleChoice or QuestionType.Cloze or QuestionType.Output => Same(answer, payload, "optionId") ? 1 : 0,
                QuestionType.OddOneOut => Same(answer, payload, "itemId") ? 1 : 0,
                QuestionType.MatchPairs => Pairs(answer, payload),
                QuestionType.Order => Ids(answer, "orderedIds").SequenceEqual(Ids(payload, "orderedIds")) ? 1 : 0,
                QuestionType.FindBug => !BugLines(answer).Contains(payload.GetProperty("line").GetInt32()) ? 0 : Same(answer, payload, "fixId") ? 1 : 0.4,
                _ => 0,
            };
        }
        catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException)
        {
            return 0;
        }
    }

    // The key's line, plus "acceptLines" when one statement spans several lines and any of them is a fair tap.
    public static IEnumerable<int> BugLines(JsonElement answer)
    {
        yield return answer.GetProperty("line").GetInt32();
        if (answer.TryGetProperty("acceptLines", out var also))
            foreach (var line in also.EnumerateArray()) yield return line.GetInt32();
    }

    // 25 % per correct pair (4 pairs); each left item counts once.
    private static double Pairs(JsonElement answer, JsonElement payload)
    {
        var key = Pairs(answer).ToDictionary(p => p.Left, p => p.Right);
        var correct = Pairs(payload).DistinctBy(p => p.Left).Count(p => key.TryGetValue(p.Left, out var right) && right == p.Right);
        return (double)correct / key.Count;
    }

    private static IEnumerable<(string Left, string Right)> Pairs(JsonElement element) =>
        element.GetProperty("pairs").EnumerateArray().Select(p => (Id(p, "leftId"), Id(p, "rightId")));

    private static IEnumerable<string> Ids(JsonElement element, string name) =>
        element.GetProperty(name).EnumerateArray().Select(e => e.GetString() ?? throw new InvalidOperationException("null id"));

    private static string Id(JsonElement element, string name) =>
        element.GetProperty(name).GetString() ?? throw new InvalidOperationException($"null {name}");

    private static bool Same(JsonElement answer, JsonElement payload, string name) =>
        payload.TryGetProperty(name, out var value) && JsonElement.DeepEquals(answer.GetProperty(name), value);
}
