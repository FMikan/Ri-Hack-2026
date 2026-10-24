using System.Net;
using App.Application.Content;

namespace App.Api.Tests;

public class ContentTests(TestApp app) : ApiTestBase(app)
{
    [Fact]
    public async Task Topics_are_public_and_sorted()
    {
        await SeedTopicsAsync(3, 1, 2);

        var response = await Client.GetAsync("/api/topics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var topics = (await ReadAsync<TopicDto[]>(response))!;
        Assert.Equal(["topic-2", "topic-3", "topic-1"], topics.Select(t => t.Slug));
        Assert.All(topics, t => Assert.Equal(2, t.ConceptCount));
    }

    [Fact]
    public async Task Concepts_of_topic_are_listed()
    {
        var topics = await SeedTopicsAsync(1);
        var session = await RegisterAsync("ana");

        var concepts = (await ReadAsync<ConceptDto[]>(await session.Client.GetAsync("/api/topics/topic-1/concepts")))!;
        var missing = await session.Client.GetAsync("/api/topics/nope/concepts");
        var anonymous = await Client.GetAsync("/api/topics/topic-1/concepts");

        Assert.Equal(["topic-1.a", "topic-1.b"], concepts.Select(c => c.Slug));
        Assert.All(concepts, c => Assert.Equal(topics[0].Id, c.TopicId));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }
}
