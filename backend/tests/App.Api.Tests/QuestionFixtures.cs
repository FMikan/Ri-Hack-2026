using System.Text.Json.Nodes;
using App.Domain.Content;
using App.Infrastructure;

namespace App.Api.Tests;

// A playable topic with synthetic questions whose answers the tests know (docs §3.2.1 shapes).
public static class QuestionFixtures
{
    private const string Four = """[{"id":"a","text":"A"},{"id":"b","text":"B"},{"id":"c","text":"C"},{"id":"d","text":"D"}]""";

    public static string ContentOf(QuestionType type) => type switch
    {
        QuestionType.TrueFalse => """{"statement":"S","answer":{"value":true}}""",
        QuestionType.MultipleChoice => $$$"""{"prompt":"P","options":{{{Four}}},"answer":{"optionId":"b"}}""",
        QuestionType.Cloze => $$$"""{"code":"x = ____","options":{{{Four}}},"answer":{"optionId":"c"}}""",
        QuestionType.OddOneOut => $$$"""{"prompt":"P","items":{{{Four}}},"answer":{"itemId":"d"}}""",
        QuestionType.Output => $$$"""{"code":"print(1)","options":{{{Four}}},"answer":{"optionId":"a"}}""",
        QuestionType.MatchPairs => """{"prompt":"P","left":[{"id":"l1","text":"1"},{"id":"l2","text":"2"},{"id":"l3","text":"3"},{"id":"l4","text":"4"}],"right":[{"id":"r1","text":"A"},{"id":"r2","text":"B"},{"id":"r3","text":"C"},{"id":"r4","text":"D"}],"answer":{"pairs":[{"leftId":"l1","rightId":"r3"},{"leftId":"l2","rightId":"r1"},{"leftId":"l3","rightId":"r4"},{"leftId":"l4","rightId":"r2"}]}}""",
        QuestionType.Order => """{"prompt":"P","steps":[{"id":"s1","text":"1"},{"id":"s2","text":"2"},{"id":"s3","text":"3"},{"id":"s4","text":"4"}],"answer":{"orderedIds":["s3","s1","s4","s2"]}}""",
        QuestionType.FindBug => """{"prompt":"P","code":"a\nb\nc\nd\ne","fixes":[{"id":"f1","text":"x"},{"id":"f2","text":"y"},{"id":"f3","text":"z"}],"answer":{"line":3,"fixId":"f2"}}""",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    public static JsonNode RightAnswer(QuestionType type) => JsonNode.Parse(ContentOf(type))!["answer"]!.DeepClone();

    public static JsonNode WrongAnswer => new JsonObject { ["nothing"] = true };

    // concepts × all eight types × D1 and D2: enough for full decks on every seed.
    public static async Task<Topic> SeedPlayableTopicAsync(AppDbContext db, string slug = "playable", int concepts = 12, DateTimeOffset? now = null)
    {
        var topic = new Topic { Slug = slug, Name = slug, Description = slug, SortOrder = 0 };
        for (var i = 0; i < concepts; i++)
            topic.Concepts.Add(new Concept { Slug = $"{slug}.c{i}", Name = $"Concept {i}", Description = "D", SortOrder = i });
        db.Topics.Add(topic);
        await db.SaveChangesAsync();
        foreach (var concept in topic.Concepts)
            foreach (var type in Enum.GetValues<QuestionType>())
                foreach (var difficulty in new[] { 1, 2 })
                    db.Questions.Add(new Question
                    {
                        TopicId = topic.Id, ConceptId = concept.Id, Type = type, Difficulty = difficulty, Language = "js",
                        Content = ContentOf(type), Remember = $"Remember {type}", CreatedAt = now ?? DateTimeOffset.UtcNow,
                    });
        await db.SaveChangesAsync();
        return topic;
    }
}
