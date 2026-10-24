using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using App.Application.Content;
using App.Domain.Content;
using App.Domain.Economy;
using App.Domain.Matches;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Seed;

// Upserts topics, concepts and questions from the JSON files embedded next to this class (Seed/topics.json,
// Seed/questions/*.json). Never deletes, and never reactivates a question that was quarantined or retired.
// "retired": true in a question's JSON retires it (its mastery changes are taken back). The file sets a question's
// difficulty once; after that auto-calibration owns it. A topic is offered only when its pool builds a complete ranked
// deck for a new player (D1–D2, nothing seen).
public class Seeder(AppDbContext db, TimeProvider time)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var now = time.GetUtcNow();
        var topics = await db.Topics.Include(t => t.Concepts).ToDictionaryAsync(t => t.Slug, ct);
        foreach (var (seed, order) in ReadTopics().Select((t, i) => (t, i)))
        {
            if (!topics.TryGetValue(seed.Slug, out var topic))
            {
                topic = new Topic { Slug = seed.Slug, Name = seed.Name, Description = seed.Description };
                db.Topics.Add(topic);
                topics[seed.Slug] = topic;
            }
            topic.Name = seed.Name;
            topic.Description = seed.Description;
            topic.SortOrder = order;
            foreach (var (conceptSeed, conceptOrder) in seed.Concepts.Select((c, i) => (c, i)))
            {
                var concept = topic.Concepts.FirstOrDefault(c => c.Slug == conceptSeed.Slug);
                if (concept is null)
                {
                    concept = new Concept { Slug = conceptSeed.Slug, Name = conceptSeed.Name, Description = conceptSeed.Description };
                    topic.Concepts.Add(concept);
                }
                concept.Name = conceptSeed.Name;
                concept.Description = conceptSeed.Description;
                concept.SortOrder = conceptOrder;
            }
        }
        await db.SaveChangesAsync(ct);

        var concepts = topics.Values.SelectMany(t => t.Concepts).ToDictionary(c => c.Slug);
        var questions = await db.Questions.Where(q => q.SeedKey != null).ToDictionaryAsync(q => q.SeedKey!, ct);
        var retire = new List<Question>();
        foreach (var seed in ReadQuestions())
        {
            var concept = concepts.GetValueOrDefault(seed.Concept)
                ?? throw new InvalidOperationException($"Seed question {seed.Key} names unknown concept {seed.Concept}.");
            if (!questions.TryGetValue(seed.Key, out var question))
            {
                question = new Question { SeedKey = seed.Key, Content = "{}", Remember = "", Difficulty = seed.Difficulty, CreatedAt = now };
                db.Questions.Add(question);
                questions[seed.Key] = question;
            }
            question.TopicId = concept.TopicId;
            question.ConceptId = concept.Id;
            question.Type = seed.Type;
            question.Language = seed.Language;
            question.Content = seed.Content.ToJsonString();
            question.Remember = seed.Remember;
            if (seed.Retired && question.Status != QuestionStatus.Retired) retire.Add(question);
        }
        await db.SaveChangesAsync(ct);
        foreach (var question in retire)
        {
            await db.RetireQuestionAsync(question.Id, now, ct);
            question.Status = QuestionStatus.Retired;
        }

        foreach (var topic in topics.Values)
        {
            var pool = questions.Values
                .Where(q => q.TopicId == topic.Id && q.Status == QuestionStatus.Active)
                .OrderBy(q => q.SeedKey)
                .Select(q => new DeckQuestion(q.Id, q.ConceptId, q.Type, q.Difficulty))
                .ToList();
            var deck = DeckBuilder.Build(pool, [], targetingAllowed: false, (1, 2), now, new Random(0), degrade: false);
            topic.IsActive = DeckBuilder.IsComplete(deck);
        }

        // The shop, upserted by key; prices and names follow the file.
        var items = await db.ShopItems.ToDictionaryAsync(i => i.Key, ct);
        foreach (var seed in Read<List<ShopSeed>>("App.Infrastructure.Seed.shop.json"))
        {
            if (!items.TryGetValue(seed.Key, out var item))
            {
                item = new ShopItem { Key = seed.Key, Kind = seed.Kind, Name = seed.Name, Price = seed.Price, AssetKey = seed.AssetKey };
                db.ShopItems.Add(item);
            }
            (item.Kind, item.Name, item.Price, item.Rarity, item.AssetKey) = (seed.Kind, seed.Name, seed.Price, seed.Rarity, seed.AssetKey);
            // A season drop is never sold: closing a season gives it to every player whose best tier it names.
            (item.SeasonDropTier, item.IsActive) = (seed.SeasonDropTier, seed.SeasonDropTier is null);
        }
        await db.SaveChangesAsync(ct);
    }

    public static List<TopicSeed> ReadTopics() => Read<List<TopicSeed>>("App.Infrastructure.Seed.topics.json");

    public static List<QuestionSeed> ReadQuestions() =>
        typeof(Seeder).Assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith("App.Infrastructure.Seed.questions.", StringComparison.Ordinal))
            .Order()
            .SelectMany(Read<List<QuestionSeed>>)
            .ToList();

    private static T Read<T>(string resource)
    {
        using var stream = typeof(Seeder).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Embedded seed file {resource} is missing.");
        return JsonSerializer.Deserialize<T>(stream, Json) ?? throw new InvalidOperationException($"Seed file {resource} is empty.");
    }
}

public record ShopSeed(string Key, ShopItemKind Kind, string Name, int Price, string Rarity, string AssetKey, string? SeasonDropTier = null);

public record TopicSeed(string Slug, string Name, string Description, List<ConceptSeed> Concepts);

public record ConceptSeed(string Slug, string Name, string Description);

public record QuestionSeed(string Key, string Concept, QuestionType Type, int Difficulty, string? Language, JsonObject Content, string Remember, bool Retired = false);
