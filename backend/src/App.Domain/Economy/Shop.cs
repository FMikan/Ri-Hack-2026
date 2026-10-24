namespace App.Domain.Economy;

public enum ShopItemKind { AvatarFrame, Banner, BoardTheme, NameColor, CardBackground, StreakFreeze }

// Cosmetics are the coin sink (idea §15); the streak freeze is the one consumable (at most 2 in stock).
public class ShopItem
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public required string Key { get; init; } // 'frame.bronze-ring', the seed's identity
    public required ShopItemKind Kind { get; set; }
    public required string Name { get; set; }
    public required int Price { get; set; }
    public string Rarity { get; set; } = "common";
    public required string AssetKey { get; set; } // the frontend maps it to an image or CSS
    public string? SeasonDropTier { get; set; }
    public bool IsActive { get; set; } = true;

    // The equipment slot an owned cosmetic goes into (users.equipment: slot → assetKey).
    public static string? SlotOf(ShopItemKind kind) => kind switch
    {
        ShopItemKind.AvatarFrame => "avatarFrame",
        ShopItemKind.Banner => "banner",
        ShopItemKind.BoardTheme => "boardTheme",
        ShopItemKind.NameColor => "nameColor",
        ShopItemKind.CardBackground => "cardBackground",
        _ => null,
    };
}

public class UserItem
{
    public required Guid UserId { get; init; }
    public required Guid ItemId { get; init; }
    public required DateTimeOffset AcquiredAt { get; init; }
}
