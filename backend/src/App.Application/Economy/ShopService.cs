using App.Application.Me;
using App.Domain;
using App.Domain.Economy;
using Microsoft.EntityFrameworkCore;

namespace App.Application.Economy;

public record ShopItemDto(Guid Id, string Key, ShopItemKind Kind, string Name, int Price, string Rarity, string AssetKey, bool Owned);

public record ShopDto(ShopItemDto[] Items);

public record PurchaseRequest
{
    public required Guid ItemId { get; init; }
}

// The coin sink (idea §15). Coins only leave through the ledger with an atomic "coins >= price" check, so two
// purchases at once can never overspend; ownership is a primary key, so an item is bought once.
public class ShopService(IAppDbContext db, Rewarder rewarder, MeService me, TimeProvider time)
{
    public async Task<ShopDto> ListAsync(Guid userId, CancellationToken ct)
    {
        var owned = (await db.UserItems.Where(i => i.UserId == userId).Select(i => i.ItemId).ToListAsync(ct)).ToHashSet();
        var items = await db.ShopItems.Where(i => i.IsActive).OrderBy(i => i.Price).ThenBy(i => i.Key).ToListAsync(ct);
        return new ShopDto(items.Select(i => new ShopItemDto(i.Id, i.Key, i.Kind, i.Name, i.Price, i.Rarity, i.AssetKey, owned.Contains(i.Id))).ToArray());
    }

    // My locker: every cosmetic I own, season drops (never sold) included, cheapest first like the shop.
    public async Task<ShopItemDto[]> InventoryAsync(Guid userId, CancellationToken ct) =>
        await db.ShopItems
            .Where(i => db.UserItems.Any(o => o.UserId == userId && o.ItemId == i.Id))
            .OrderBy(i => i.Price).ThenBy(i => i.Key)
            .Select(i => new ShopItemDto(i.Id, i.Key, i.Kind, i.Name, i.Price, i.Rarity, i.AssetKey, true))
            .ToArrayAsync(ct);

    public async Task<MeDto> PurchaseAsync(Guid userId, Guid itemId, CancellationToken ct)
    {
        var item = await db.ShopItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == itemId && i.IsActive, ct)
            ?? throw new AppException(ErrorCodes.ShopItemNotFound, "This item is not for sale.", 404);
        var now = time.GetUtcNow();
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            if (item.Kind == ShopItemKind.StreakFreeze)
            {
                // At most two in stock; the conditional update keeps the limit under concurrent purchases.
                if (await db.Users.AnyAsync(u => u.Id == userId && u.StreakFreezes >= GameRules.MaxStreakFreezes, ct)) throw FreezeLimit();
                await SpendAsync(userId, item, RewardType.StreakFreeze, $"streak_freeze:{userId}:{Guid.CreateVersion7()}", ct);
                if (await db.Users.Where(u => u.Id == userId && u.StreakFreezes < GameRules.MaxStreakFreezes)
                        .ExecuteUpdateAsync(s => s.SetProperty(u => u.StreakFreezes, u => u.StreakFreezes + 1), ct) == 0)
                    throw FreezeLimit();
            }
            else
            {
                if (await db.Database.ExecuteSqlAsync(
                        $"insert into user_items (user_id, item_id, acquired_at) values ({userId}, {item.Id}, {now}) on conflict do nothing", ct) == 0)
                    throw new AppException(ErrorCodes.ShopAlreadyOwned, "You already own this.");
                await SpendAsync(userId, item, RewardType.ShopPurchase, $"shop_purchase:{userId}:{item.Id}", ct);
            }
            await transaction.CommitAsync(ct);
        }
        return await me.GetAsync(userId, ct);
    }

    private async Task SpendAsync(Guid userId, ShopItem item, RewardType type, string dedupeKey, CancellationToken ct)
    {
        if (!await rewarder.SpendAsync(userId, type, item.Price, item.Id, dedupeKey, ct))
            throw new AppException(ErrorCodes.ShopInsufficientCoins, $"You need {item.Price} coins.");
    }

    private static AppException FreezeLimit() => new(ErrorCodes.ShopFreezeLimit, $"You can keep at most {GameRules.MaxStreakFreezes} streak freezes.");
}
