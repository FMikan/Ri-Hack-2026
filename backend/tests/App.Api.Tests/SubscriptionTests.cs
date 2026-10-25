using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using App.Application.Economy;
using App.Application.Me;
using App.Application.Professor;
using App.Application.Subscription;
using App.Domain;
using App.Domain.Economy;
using App.Domain.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace App.Api.Tests;

// The Senior developer subscription (docs/premium-pretplata.md): a simulated card payment, the perks it switches on
// and the Junior developer's daily AI Professor allowance.
public class SubscriptionTests(TestApp app) : ApiTestBase(app)
{
    private const string Visa = "4242 4242 4242 4242", Declined = "4000 0000 0000 0002";

    private Task<HttpResponseMessage> SubscribeAsync(Session session, string interval = "monthly", string number = Visa) =>
        PostAsync(session.Client, "/api/subscription", new { interval, cardHolder = "Ana Anić", cardNumber = number, cardExpiry = "12/28", cardCvc = "123" });

    private async Task<SubscriptionDto> SubscribedAsync(Session session, string interval = "monthly")
    {
        var response = await SubscribeAsync(session, interval);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ReadAsync<SubscriptionDto>(response))!;
    }

    private async Task<SubscriptionDto> StatusAsync(Session session) =>
        (await session.Client.GetFromJsonAsync<SubscriptionDto>("/api/subscription", App.Json))!;

    private async Task<string?> ErrorAsync(HttpResponseMessage response) => (await ReadAsync<ProblemDetails>(response))!.Type;

    private Task SeedShopAsync() => App.InDbAsync(async db =>
    {
        db.ShopItems.AddRange(
            new ShopItem { Key = Premium.WelcomePack[0], Kind = ShopItemKind.AvatarFrame, Name = "Event loop", Price = 6000, Rarity = "legendary", AssetKey = "frame-event-loop" },
            new ShopItem { Key = Premium.WelcomePack[1], Kind = ShopItemKind.NameColor, Name = "Gold master", Price = 6000, Rarity = "legendary", AssetKey = "name-gold-master" },
            new ShopItem { Key = "frame.ring", Kind = ShopItemKind.AvatarFrame, Name = "Ring", Price = 300, AssetKey = "frame-ring" });
        await db.SaveChangesAsync();
        return 0;
    });

    private async Task<Grant?> GrantAsync(Session session, Grant grant)
    {
        using var scope = App.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<Rewarder>().GrantAsync(session.Auth.User.Id, grant, default);
    }

    private Task<User> UserAsync(Session session) => App.InDbAsync(db => db.Users.AsNoTracking().FirstAsync(u => u.Id == session.Auth.User.Id));

    [Fact]
    public async Task Paying_makes_me_a_senior_developer_with_the_welcome_pack()
    {
        await SeedShopAsync();
        var ana = await RegisterAsync("ana");
        var now = App.Clock.GetUtcNow();
        Assert.Equal(Plan.Junior, (await StatusAsync(ana)).Plan);

        var status = await SubscribedAsync(ana);

        Assert.Equal((Plan.Senior, now.AddDays(30), "monthly", true, "4242"), (status.Plan, status.PremiumUntil, status.Interval, status.AutoRenew, status.CardLast4));
        Assert.Equal([499, 3999], status.Plans.Select(p => p.PriceCents));
        Assert.Null(status.Professor.QuestionsLimit);
        var me = (await ana.Client.GetFromJsonAsync<MeDto>("/api/me", App.Json))!;
        Assert.Equal((Plan.Senior, now.AddDays(30)), (me.Plan, me.PremiumUntil));
        var locker = (await ana.Client.GetFromJsonAsync<ShopItemDto[]>("/api/me/items", App.Json))!;
        Assert.Equal(Premium.WelcomePack[..2].Order(), locker.Select(i => i.Key).Order()); // the pack, nothing else
    }

    [Fact]
    public async Task A_declined_card_is_402_and_changes_nothing()
    {
        var ana = await RegisterAsync("ana");

        var response = await SubscribeAsync(ana, number: Declined);

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.Equal(ErrorCodes.PaymentDeclined, await ErrorAsync(response));
        Assert.Equal(Plan.Junior, (await StatusAsync(ana)).Plan);
    }

    [Fact]
    public async Task A_bad_card_or_interval_is_400()
    {
        var ana = await RegisterAsync("ana");

        var interval = await SubscribeAsync(ana, interval: "weekly");
        var number = await SubscribeAsync(ana, number: "12");

        Assert.Equal(HttpStatusCode.BadRequest, interval.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, number.StatusCode);
    }

    [Fact]
    public async Task Paying_again_adds_the_time_on_top_and_cancelling_keeps_senior_until_the_end()
    {
        var ana = await RegisterAsync("ana");
        var now = App.Clock.GetUtcNow();
        await SubscribedAsync(ana);

        var yearly = await SubscribedAsync(ana, "yearly");
        Assert.Equal((now.AddDays(30 + 365), "yearly"), (yearly.PremiumUntil, yearly.Interval));

        var cancelled = (await ReadAsync<SubscriptionDto>(await ana.Client.DeleteAsync("/api/subscription")))!;
        Assert.Equal((Plan.Senior, false), (cancelled.Plan, cancelled.AutoRenew));

        App.Clock.Advance(TimeSpan.FromDays(30 + 365 + 1));
        ana = await LoginAgainAsync(ana);
        Assert.Equal(Plan.Junior, (await StatusAsync(ana)).Plan);
        Assert.Equal(ErrorCodes.SubscriptionNotActive, await ErrorAsync(await ana.Client.DeleteAsync("/api/subscription")));
    }

    [Fact]
    public async Task Daily_coins_are_for_seniors_once_a_day()
    {
        var ana = await RegisterAsync("ana");
        var claim = () => ana.Client.PostAsync("/api/subscription/daily-coins", null);

        Assert.Equal(ErrorCodes.SubscriptionSeniorOnly, await ErrorAsync(await claim()));
        await SubscribedAsync(ana);
        Assert.False((await StatusAsync(ana)).DailyCoinsClaimed);

        var first = (await ReadAsync<MeDto>(await claim()))!;
        var again = (await ReadAsync<MeDto>(await claim()))!;

        Assert.Equal((25, 25), (first.Coins, again.Coins));
        Assert.True((await StatusAsync(ana)).DailyCoinsClaimed);
        App.Clock.Advance(TimeSpan.FromDays(1));
        ana = await LoginAgainAsync(ana);
        Assert.Equal(50, (await ReadAsync<MeDto>(await claim()))!.Coins);
        var ledger = await App.InDbAsync(db => db.RewardEvents.Where(e => e.UserId == ana.Auth.User.Id && e.Type == RewardType.PremiumDaily).CountAsync());
        Assert.Equal(2, ledger);
    }

    [Fact]
    public async Task Senior_xp_is_20_percent_larger_in_the_ledger_and_the_cache()
    {
        var ana = await RegisterAsync("ana");
        var ben = await RegisterAsync("ben");
        await SubscribedAsync(ana);

        var paid = await GrantAsync(ana, new Grant(RewardType.MatchXp, 0, 50, 0, null, "test:ana-xp"));
        await GrantAsync(ben, new Grant(RewardType.MatchXp, 0, 50, 0, null, "test:ben-xp"));
        await GrantAsync(ana, new Grant(RewardType.MatchWin, 15, 0, 0, null, "test:ana-coins"));

        Assert.Equal(60, paid!.Xp);
        Assert.Equal((60, 15), ((await UserAsync(ana)).Xp, (await UserAsync(ana)).Coins)); // coins are not multiplied
        Assert.Equal(50, (await UserAsync(ben)).Xp);
        Assert.Equal(60, await App.InDbAsync(db => db.RewardEvents.Where(e => e.UserId == ana.Auth.User.Id).SumAsync(e => e.Xp)));
    }

    [Fact]
    public async Task Junior_gets_2_questions_1_lesson_and_1_quiz_a_day_and_senior_has_no_limit()
    {
        var ana = await RegisterAsync("ana");
        var ask = (Guid? chat) => ana.Client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/professor/messages")
        {
            Content = JsonContent.Create(new { chatId = chat, text = "How does DNS work?" }, options: App.Json),
            Headers = { { "Accept", "text/event-stream, application/problem+json" } },
        });
        async Task<Guid> AnswerIdAsync(HttpResponseMessage response)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var done = (await response.Content.ReadAsStringAsync()).Split("\n\n", StringSplitOptions.RemoveEmptyEntries)[^1];
            var data = string.Join('\n', done.Split('\n').Where(l => l.StartsWith("data:")).Select(l => l["data:".Length..].TrimStart()));
            return JsonSerializer.Deserialize<ProfessorTurnDto>(data, App.Json)!.Answer.Id;
        }

        var first = await AnswerIdAsync(await ask(null));
        var second = await AnswerIdAsync(await ask(null));
        var third = await ask(null);
        Assert.Equal(HttpStatusCode.Conflict, third.StatusCode);
        Assert.Equal(ErrorCodes.ProfessorQuota, await ErrorAsync(third));
        Assert.Equal((2, 2, 0, 1, 0, 1), Usage(await StatusAsync(ana)));

        Assert.Equal(HttpStatusCode.OK, (await PostAsync(ana.Client, "/api/professor/lessons", new { messageId = first })).StatusCode);
        Assert.Equal(ErrorCodes.ProfessorQuota, await ErrorAsync(await PostAsync(ana.Client, "/api/professor/lessons", new { messageId = second })));
        // An answer that already has its lesson shows it again without writing one.
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(ana.Client, "/api/professor/lessons", new { messageId = first })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(ana.Client, "/api/professor/quizzes", new { messageId = first })).StatusCode);
        Assert.Equal(ErrorCodes.ProfessorQuota, await ErrorAsync(await PostAsync(ana.Client, "/api/professor/quizzes", new { messageId = second })));
        Assert.Equal((2, 2, 1, 1, 1, 1), Usage(await StatusAsync(ana)));

        await SubscribedAsync(ana);
        await AnswerIdAsync(await ask(null));
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(ana.Client, "/api/professor/lessons", new { messageId = second })).StatusCode);
        Assert.Equal((3, null, 2, null, 1, null), Usage(await StatusAsync(ana)));

        // The allowance is per day: tomorrow a Junior starts again.
        var ben = await RegisterAsync("ben");
        await AnswerIdAsync(await ask(null)); // ana, senior: still fine
        await AnswerIdAsync(await Ask(ben));
        await AnswerIdAsync(await Ask(ben));
        Assert.Equal(HttpStatusCode.Conflict, (await Ask(ben)).StatusCode);
        App.Clock.Advance(TimeSpan.FromDays(1));
        ben = await LoginAgainAsync(ben);
        await AnswerIdAsync(await Ask(ben));

        Task<HttpResponseMessage> Ask(Session s) => s.Client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/professor/messages")
        {
            Content = JsonContent.Create(new { chatId = (Guid?)null, text = "What is TCP?" }, options: App.Json),
            Headers = { { "Accept", "text/event-stream, application/problem+json" } },
        });
    }

    private static (int, int?, int, int?, int, int?) Usage(SubscriptionDto s) =>
        (s.Professor.QuestionsUsed, s.Professor.QuestionsLimit, s.Professor.LessonsUsed, s.Professor.LessonsLimit, s.Professor.QuizzesUsed, s.Professor.QuizzesLimit);
}
