using System.Net;
using System.Net.Http.Json;
using App.Application.Economy;
using App.Application.Me;
using App.Domain;
using App.Domain.Economy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace App.Api.Tests;

// F4b: the shop (idea §15): cosmetics, the streak freeze and equipment.
public class ShopTests(TestApp app) : ApiTestBase(app)
{
    private Task<List<ShopItem>> SeedShopAsync() => App.InDbAsync(async db =>
    {
        List<ShopItem> items =
        [
            new() { Key = "frame.ring", Kind = ShopItemKind.AvatarFrame, Name = "Ring", Price = 300, AssetKey = "frame-ring" },
            new() { Key = "banner.terminal", Kind = ShopItemKind.Banner, Name = "Terminal", Price = 400, AssetKey = "banner-terminal" },
            new() { Key = "streak-freeze", Kind = ShopItemKind.StreakFreeze, Name = "Streak freeze", Price = 100, AssetKey = "freeze" },
        ];
        db.ShopItems.AddRange(items);
        await db.SaveChangesAsync();
        return items;
    });

    private async Task GiveCoinsAsync(Session session, int coins)
    {
        using var scope = App.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<Rewarder>().GrantAsync(session.Auth.User.Id, new Grant(RewardType.MatchWin, coins, 0, 0, null, $"test:{Guid.NewGuid()}"), default);
    }

    private Task<HttpResponseMessage> BuyAsync(Session session, ShopItem item) => PostAsync(session.Client, "/api/shop/purchase", new { itemId = item.Id });

    private async Task<string?> ErrorAsync(HttpResponseMessage response) => (await ReadAsync<ProblemDetails>(response))!.Type;

    private async Task AssertLedgerMatchesAsync(Session session)
    {
        var (coins, ledger) = await App.InDbAsync(async db => (
            await db.Users.Where(u => u.Id == session.Auth.User.Id).Select(u => u.Coins).FirstAsync(),
            await db.RewardEvents.Where(e => e.UserId == session.Auth.User.Id).SumAsync(e => e.Coins)));
        Assert.Equal(ledger, coins);
    }

    [Fact]
    public async Task Purchase_deducts_coins_and_adds_item()
    {
        var items = await SeedShopAsync();
        var ana = await RegisterAsync("ana");
        await GiveCoinsAsync(ana, 1000);

        var response = await BuyAsync(ana, items[0]);
        var shop = (await ana.Client.GetFromJsonAsync<ShopDto>("/api/shop", App.Json))!;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(700, (await ReadAsync<MeDto>(response))!.Coins);
        Assert.Equal(["frame.ring"], shop.Items.Where(i => i.Owned).Select(i => i.Key));
        Assert.Equal(shop.Items.Select(i => i.Price).Order(), shop.Items.Select(i => i.Price)); // cheapest first
        Assert.Equal(ErrorCodes.ShopAlreadyOwned, await ErrorAsync(await BuyAsync(ana, items[0])));
        await AssertLedgerMatchesAsync(ana);
    }

    [Fact]
    public async Task Insufficient_coins_is_409()
    {
        var items = await SeedShopAsync();
        var ana = await RegisterAsync("ana");
        await GiveCoinsAsync(ana, 299);

        var response = await BuyAsync(ana, items[0]);

        Assert.Equal(ErrorCodes.ShopInsufficientCoins, await ErrorAsync(response));
        Assert.DoesNotContain((await ana.Client.GetFromJsonAsync<ShopDto>("/api/shop", App.Json))!.Items, i => i.Owned);
        await AssertLedgerMatchesAsync(ana);
    }

    [Fact]
    public async Task Concurrent_purchases_cannot_overspend()
    {
        var items = await SeedShopAsync();
        var ana = await RegisterAsync("ana");
        await GiveCoinsAsync(ana, 500);

        var responses = await Task.WhenAll(BuyAsync(ana, items[0]), BuyAsync(ana, items[1]));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        var me = (await ana.Client.GetFromJsonAsync<MeDto>("/api/me", App.Json))!;
        Assert.True(me.Coins is 200 or 100);
        await AssertLedgerMatchesAsync(ana);
    }

    [Fact]
    public async Task Buy_streak_freeze_max_2()
    {
        var items = await SeedShopAsync();
        var ana = await RegisterAsync("ana");
        await GiveCoinsAsync(ana, 1000);

        await BuyAsync(ana, items[2]);
        var second = (await ReadAsync<MeDto>(await BuyAsync(ana, items[2])))!;
        var third = await BuyAsync(ana, items[2]);

        Assert.Equal((2, 800), (second.Streak.Freezes, second.Coins));
        Assert.Equal(ErrorCodes.ShopFreezeLimit, await ErrorAsync(third));
        await AssertLedgerMatchesAsync(ana);
    }

    [Fact]
    public async Task Equip_requires_ownership_and_null_takes_it_off()
    {
        var items = await SeedShopAsync();
        var ana = await RegisterAsync("ana");
        await GiveCoinsAsync(ana, 1000);

        var notOwned = await ana.Client.PutAsJsonAsync("/api/me/equipment", new { avatarFrame = items[0].Id }, App.Json);
        await BuyAsync(ana, items[0]);
        var equipped = (await ReadAsync<MeDto>(await ana.Client.PutAsJsonAsync("/api/me/equipment", new { avatarFrame = items[0].Id }, App.Json)))!;
        var off = (await ReadAsync<MeDto>(await ana.Client.PutAsJsonAsync("/api/me/equipment", new { avatarFrame = (Guid?)null }, App.Json)))!;

        Assert.Equal(ErrorCodes.ShopNotOwned, await ErrorAsync(notOwned));
        Assert.Equal("frame-ring", equipped.Equipment["avatarFrame"]);
        Assert.False(off.Equipment.ContainsKey("avatarFrame"));
    }

    // The locker on the profile: every cosmetic I own, season drops (never for sale) included; the streak freeze is not an item I keep.
    [Fact]
    public async Task Inventory_lists_owned_cosmetics_with_season_drops()
    {
        var items = await SeedShopAsync();
        var drop = await App.InDbAsync(async db =>
        {
            var item = new ShopItem { Key = "frame.season-gold", Kind = ShopItemKind.AvatarFrame, Name = "Season gold", Price = 1000, AssetKey = "frame-season-gold", SeasonDropTier = "gold", IsActive = false };
            db.ShopItems.Add(item);
            await db.SaveChangesAsync();
            return item;
        });
        var ana = await RegisterAsync("ana");
        var bob = await RegisterAsync("bob");
        await GiveCoinsAsync(ana, 1000);
        await App.InDbAsync(async db =>
        {
            db.UserItems.Add(new UserItem { UserId = ana.Auth.User.Id, ItemId = drop.Id, AcquiredAt = DateTimeOffset.UtcNow });
            return await db.SaveChangesAsync();
        });

        await BuyAsync(ana, items[1]);
        await BuyAsync(ana, items[2]); // a streak freeze
        var mine = (await ana.Client.GetFromJsonAsync<ShopItemDto[]>("/api/me/items", App.Json))!;
        var none = (await bob.Client.GetFromJsonAsync<ShopItemDto[]>("/api/me/items", App.Json))!;

        Assert.Equal(["banner.terminal", "frame.season-gold"], mine.Select(i => i.Key));
        Assert.All(mine, i => Assert.True(i.Owned));
        Assert.Empty(none);
    }
}
