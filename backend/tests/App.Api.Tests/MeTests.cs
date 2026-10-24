using System.Net;
using System.Net.Http.Json;
using App.Domain;
using App.Application.Me;
using App.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Tests;

public class MeTests(TestApp app) : ApiTestBase(app)
{
    [Fact]
    public async Task Me_returns_defaults()
    {
        var session = await RegisterAsync("ana");

        var me = await ReadAsync<MeDto>(await session.Client.GetAsync("/api/me"));

        Assert.Equal(1000, me!.Rating);
        Assert.True(me.IsProvisional);
        Assert.Equal("bronze", me.Tier);
        Assert.Equal(0, me.Coins);
        Assert.Equal(1, me.Level);
        Assert.Equal(200, me.XpToNextLevel);
        Assert.Equal(0, me.Streak.Count);
        Assert.Empty(me.ActiveTopicIds);
        Assert.Equal("en", me.Locale);
        Assert.Equal(ProfileVisibility.Public, me.ProfileVisibility);
        Assert.True(me.Notifications["dailyChallenge"]);
    }

    [Fact]
    public async Task Patch_me_updates_locale_and_visibility()
    {
        var session = await RegisterAsync("ana");

        var response = await session.Client.PatchAsJsonAsync("/api/me", new { locale = "hr", profileVisibility = "friends", avatar = "fox" }, App.Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await ReadAsync<MeDto>(response);
        Assert.Equal("hr", me!.Locale);
        Assert.Equal(ProfileVisibility.Friends, me.ProfileVisibility);
        Assert.Equal("fox", me.Avatar);

        var invalid = await session.Client.PatchAsJsonAsync("/api/me", new { locale = "de" }, App.Json);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Set_topics_requires_3_to_5()
    {
        var topics = await SeedTopicsAsync(1, 2, 3, 4, 5, 6);
        var session = await RegisterAsync("ana");
        var ids = topics.Select(t => t.Id).ToArray();

        var two = await session.Client.PutAsJsonAsync("/api/me/topics", new SetTopicsRequest { TopicIds = ids[..2] }, App.Json);
        var six = await session.Client.PutAsJsonAsync("/api/me/topics", new SetTopicsRequest { TopicIds = ids }, App.Json);
        var three = await session.Client.PutAsJsonAsync("/api/me/topics", new SetTopicsRequest { TopicIds = ids[..3] }, App.Json);

        Assert.Equal(HttpStatusCode.Conflict, two.StatusCode);
        Assert.Equal(ErrorCodes.TopicsCount, (await ReadAsync<ProblemDetails>(two))!.Type);
        Assert.Equal(HttpStatusCode.Conflict, six.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, three.StatusCode);
        var me = await ReadAsync<MeDto>(await session.Client.GetAsync("/api/me"));
        Assert.Equal(ids[..3].Order(), me!.ActiveTopicIds.Order());
    }

    [Fact]
    public async Task Set_topics_with_unknown_topic_is_404()
    {
        var topics = await SeedTopicsAsync(1, 2);
        var session = await RegisterAsync("ana");

        var response = await session.Client.PutAsJsonAsync("/api/me/topics", new SetTopicsRequest { TopicIds = [topics[0].Id, topics[1].Id, Guid.NewGuid()] }, App.Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_returns_topics_with_active_flags_and_due_count()
    {
        var topics = await SeedTopicsAsync(1, 2, 3, 4);
        var session = await RegisterAsync("ana");
        await session.Client.PutAsJsonAsync("/api/me/topics", new SetTopicsRequest { TopicIds = topics.Take(3).Select(t => t.Id).ToArray() }, App.Json);

        var dashboard = await ReadAsync<DashboardDto>(await session.Client.GetAsync("/api/me/dashboard"));

        Assert.Equal(4, dashboard!.Topics.Length);
        Assert.Equal([true, true, true, false], dashboard.Topics.Select(t => t.Active));
        Assert.All(dashboard.Topics, t => Assert.Null(t.Mastery));
        Assert.Equal(0, dashboard.DueReviews);
        Assert.Equal(3, dashboard.Me.ActiveTopicIds.Length);
        Assert.False(dashboard.QueueLocked);
        Assert.Empty(dashboard.RatingHistory);
    }
}
