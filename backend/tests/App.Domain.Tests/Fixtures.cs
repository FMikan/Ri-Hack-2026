using System.Text.Json;
using App.Domain.Content;
using App.Domain.Matches;
using App.Domain.Progress;

namespace App.Domain.Tests;

// Minimal valid content per question type (docs §3.2.1), with known answers.
public static class Fixtures
{
    public static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    private const string Four = """[{"id":"a","text":"A"},{"id":"b","text":"B"},{"id":"c","text":"C"},{"id":"d","text":"D"}]""";

    public static string ContentOf(QuestionType type) => type switch
    {
        QuestionType.TrueFalse => """{"statement":"S","answer":{"value":true}}""",
        QuestionType.MultipleChoice => $$$"""{"prompt":"P","options":{{{Four}}},"answer":{"optionId":"b"}}""",
        QuestionType.Cloze => $$$"""{"code":"const x = ____;","options":{{{Four}}},"answer":{"optionId":"c"}}""",
        QuestionType.OddOneOut => $$$"""{"prompt":"P","items":{{{Four}}},"answer":{"itemId":"d"}}""",
        QuestionType.Output => $$$"""{"code":"print(1)","options":{{{Four}}},"answer":{"optionId":"a"}}""",
        QuestionType.MatchPairs => """
            {"prompt":"P","left":[{"id":"l1","text":"1"},{"id":"l2","text":"2"},{"id":"l3","text":"3"},{"id":"l4","text":"4"}],
             "right":[{"id":"r1","text":"A"},{"id":"r2","text":"B"},{"id":"r3","text":"C"},{"id":"r4","text":"D"}],
             "answer":{"pairs":[{"leftId":"l1","rightId":"r3"},{"leftId":"l2","rightId":"r1"},{"leftId":"l3","rightId":"r4"},{"leftId":"l4","rightId":"r2"}]}}
            """,
        QuestionType.Order => """{"prompt":"P","steps":[{"id":"s1","text":"1"},{"id":"s2","text":"2"},{"id":"s3","text":"3"},{"id":"s4","text":"4"}],"answer":{"orderedIds":["s3","s1","s4","s2"]}}""",
        QuestionType.FindBug => """{"prompt":"P","code":"a\nb\nc\nd\ne","fixes":[{"id":"f1","text":"x"},{"id":"f2","text":"y"},{"id":"f3","text":"z"}],"answer":{"line":3,"fixId":"f2"}}""",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    public static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    public static JsonElement Answer(QuestionType type) => QuestionJson.Answer(ContentOf(type));

    // concepts × every type × every given difficulty.
    public static TopicPool Pool(int concepts, params int[] difficulties)
    {
        var conceptIds = Enumerable.Range(0, concepts).Select(i => new Guid(i + 1, 0, 0, new byte[8])).ToList();
        var questions = (
            from concept in conceptIds
            from type in Enum.GetValues<QuestionType>()
            from difficulty in difficulties
            select new Question
            {
                ConceptId = concept, Type = type, Difficulty = difficulty, Content = ContentOf(type), Remember = "R", CreatedAt = Now,
            }).ToList();
        return new TopicPool(conceptIds, questions);
    }

    public static DeckPlayer Player(Dictionary<Guid, ConceptMastery>? mastery = null, IEnumerable<Guid>? seen = null, IEnumerable<Guid>? recentlyTargeted = null) =>
        new(mastery ?? [], (seen ?? []).ToHashSet(), (recentlyTargeted ?? []).ToHashSet());

    public static ConceptMastery Concept(Guid concept, double mastery, int answers = 3, DateTimeOffset? dueAt = null) =>
        new() { UserId = Guid.Empty, ConceptId = concept, TopicId = Guid.Empty, Mastery = mastery, AnswerCount = answers, ReviewDueAt = dueAt, ReviewStep = dueAt is null ? null : 1 };
}

public record TopicPool(List<Guid> Concepts, List<Question> Questions)
{
    public List<DeckQuestion> Deck => Questions.Select(q => new DeckQuestion(q.Id, q.ConceptId, q.Type, q.Difficulty)).ToList();
    public Dictionary<Guid, Question> ById => Questions.ToDictionary(q => q.Id);
}
