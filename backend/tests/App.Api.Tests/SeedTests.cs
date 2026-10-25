using System.Text.Json;
using System.Text.Json.Nodes;
using App.Domain.Content;
using App.Domain.Economy;
using App.Domain.Matches;
using App.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace App.Api.Tests;

// The embedded seed (App.Infrastructure/Seed) is the question pool players see: these are its quality gates (docs §7).
public class SeedTests(TestApp app) : ApiTestBase(app)
{
    // Every topic in topics.json must be playable on every band.
    private static readonly string[] DeepTopics = [.. Seeder.ReadTopics().Select(t => t.Slug)];

    private async Task SeedAsync()
    {
        using var scope = App.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<Seeder>().SeedAsync();
    }

    // The shop (idea §15): 8 to 15 cosmetics for sale per slot, rarities the frontend names, keys and looks unique.
    [Fact]
    public void Shop_seed_is_well_formed()
    {
        var shop = Seeder.ReadShop();

        Assert.Equal(shop.Count, shop.Select(i => i.Key).Distinct().Count());
        Assert.Equal(shop.Count, shop.Select(i => i.AssetKey).Distinct().Count());
        Assert.All(shop, i => Assert.Contains(i.Rarity, (string[])["common", "rare", "epic", "legendary"]));
        Assert.All(shop, i => Assert.True(i.Price > 0, i.Key));
        foreach (var kind in Enum.GetValues<ShopItemKind>().Where(k => ShopItem.SlotOf(k) is not null))
            Assert.InRange(shop.Count(i => i.Kind == kind && i.SeasonDropTier is null), 8, 15);
    }

    [Fact]
    public async Task Seed_is_idempotent()
    {
        await SeedAsync();
        var first = await App.InDbAsync(async db => (await db.Topics.CountAsync(), await db.Concepts.CountAsync(), await db.Questions.CountAsync()));
        await SeedAsync();
        var second = await App.InDbAsync(async db => (await db.Topics.CountAsync(), await db.Concepts.CountAsync(), await db.Questions.CountAsync()));

        Assert.Equal(first, second);
        Assert.Equal(Seeder.ReadQuestions().Count, second.Item3);
    }

    // A content update that adds a concept to a topic the database already has (a live database, not a fresh one).
    [Fact]
    public async Task Seed_adds_a_new_concept_to_an_existing_topic()
    {
        await SeedAsync();
        var slug = Seeder.ReadTopics()[0].Concepts[0].Slug;
        await App.InDbAsync(async db =>
        {
            var conceptId = await db.Concepts.Where(c => c.Slug == slug).Select(c => c.Id).SingleAsync();
            await db.Questions.Where(q => q.ConceptId == conceptId).ExecuteDeleteAsync();
            return await db.Concepts.Where(c => c.Id == conceptId).ExecuteDeleteAsync();
        });

        await SeedAsync();

        Assert.True(await App.InDbAsync(db => db.Concepts.AnyAsync(c => c.Slug == slug)));
        Assert.Equal(Seeder.ReadQuestions().Count, await App.InDbAsync(db => db.Questions.CountAsync()));
    }

    [Fact]
    public async Task Every_listed_topic_is_playable_and_nothing_else_is()
    {
        await App.InDbAsync(db => QuestionFixtures.SeedPlayableTopicAsync(db, slug: "dropped-topic")); // no longer in topics.json
        await SeedAsync();

        var active = await App.InDbAsync(db => db.Topics.Where(t => t.IsActive).Select(t => t.Slug).ToListAsync());
        var withQuestions = Seeder.ReadQuestions().Select(q => q.Concept).Distinct()
            .Select(c => Seeder.ReadTopics().First(t => t.Concepts.Any(x => x.Slug == c)).Slug).Distinct();

        Assert.All(DeepTopics, slug => Assert.Contains(slug, active));
        Assert.All(active, slug => Assert.Contains(slug, withQuestions));
        Assert.DoesNotContain("dropped-topic", active);
    }

    [Fact]
    public void Deep_topics_build_complete_decks_on_every_band()
    {
        var conceptTopic = Seeder.ReadTopics().SelectMany(t => t.Concepts.Select(c => (c.Slug, Topic: t.Slug))).ToDictionary(x => x.Slug, x => x.Topic);
        foreach (var topic in DeepTopics)
        {
            var pool = Seeder.ReadQuestions()
                .Where(q => conceptTopic[q.Concept] == topic)
                .Select(q => new DeckQuestion(Guid.NewGuid(), new Guid(q.Concept.GetHashCode(), 0, 0, new byte[8]), q.Type, q.Difficulty))
                .ToList();
            foreach (var band in new[] { (1, 2), (2, 3), (3, 4) })
                foreach (var seed in Enumerable.Range(0, 25))
                {
                    var deck = DeckBuilder.Build(pool, [], targetingAllowed: false, band, DateTimeOffset.UtcNow, new Random(seed), degrade: false);
                    Assert.True(DeckBuilder.IsComplete(deck), $"{topic} band {band} seed {seed}");
                }
        }
    }

    [Fact]
    public void Every_seed_question_is_well_formed()
    {
        var concepts = Seeder.ReadTopics().SelectMany(t => t.Concepts).Select(c => c.Slug).ToHashSet();
        var questions = Seeder.ReadQuestions();
        var problems = new List<string>();

        foreach (var duplicate in questions.GroupBy(q => q.Key).Where(g => g.Count() > 1))
            problems.Add($"{duplicate.Key}: key used {duplicate.Count()} times");

        foreach (var q in questions)
        {
            void Problem(string message) => problems.Add($"{q.Key}: {message}");
            var content = q.Content;
            var answer = content["answer"] as JsonObject;

            if (!concepts.Contains(q.Concept)) Problem($"unknown concept {q.Concept}");
            if (q.Difficulty is < 1 or > 4) Problem("difficulty must be 1-4");
            if (string.IsNullOrWhiteSpace(q.Remember)) Problem("remember is empty");
            if (content.ContainsKey("type")) Problem("content must not contain type");
            if (answer is null)
            {
                Problem("content.answer is missing");
                continue;
            }

            if (content["code"] is JsonValue code)
            {
                var lines = code.GetValue<string>().Split('\n');
                if (lines.Length > 25) Problem($"code has {lines.Length} lines");
                if (lines.Any(l => l.Length > 60)) Problem("a code line is longer than 60 characters");
                if (string.IsNullOrEmpty(q.Language)) Problem("code without language");
                if (q.Type == QuestionType.Cloze && code.GetValue<string>().Split("____").Length != 2) Problem("cloze needs exactly one ____");
                if (q.Type == QuestionType.FindBug && (answer["line"]?.GetValue<int>() is not { } bugLine || bugLine < 1 || bugLine > lines.Length))
                    Problem("find_bug line is outside the code");
            }
            else if (q.Type is QuestionType.Cloze or QuestionType.Output or QuestionType.FindBug)
                Problem("code is required");

            var ids = q.Type switch
            {
                QuestionType.MultipleChoice or QuestionType.Cloze or QuestionType.Output => Ids(content, "options"),
                QuestionType.OddOneOut => Ids(content, "items"),
                QuestionType.FindBug => Ids(content, "fixes"),
                QuestionType.Order => Ids(content, "steps"),
                QuestionType.MatchPairs => [.. Ids(content, "left"), .. Ids(content, "right")],
                _ => [],
            };
            if (ids.Distinct().Count() != ids.Count) Problem("ids are not unique");

            // The key must grade as fully correct, and a bot's wrong answer as zero.
            var key = Element(answer);
            if (Grader.Grade(q.Type, key, key) != 1) Problem("the key does not grade as correct");
            var (wrong, _) = BotPlayer.Answer(q.Type, content.ToJsonString(), 1, BotPersona.VibeCoder, 60_000, new Random(1));
            var wrongFraction = Grader.Grade(q.Type, key, Element(wrong));
            if (wrongFraction is not (0 or 1)) Problem($"a bot's wrong answer scores {wrongFraction}");

            if (q.Type == QuestionType.MatchPairs)
            {
                var left = Ids(content, "left");
                var right = Ids(content, "right");
                var pairs = answer["pairs"]!.AsArray().ToDictionary(p => p!["leftId"]!.GetValue<string>(), p => p!["rightId"]!.GetValue<string>());
                if (left.Where((l, i) => i < right.Count && pairs.GetValueOrDefault(l) == right[i]).Count() > 1)
                    Problem("the right list lines up with its partners; shuffle it");
            }
            if (q.Type == QuestionType.Order && Ids(content, "steps").SequenceEqual(answer["orderedIds"]!.AsArray().Select(n => n!.GetValue<string>())))
                Problem("steps are already in the correct order");
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    private static List<string> Ids(JsonObject content, string name) =>
        content[name]?.AsArray().Select(o => o!["id"]!.GetValue<string>()).ToList() ?? [];

    private static JsonElement Element(JsonNode node)
    {
        using var document = JsonDocument.Parse(node.ToJsonString());
        return document.RootElement.Clone();
    }
}
