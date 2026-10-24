using App.Domain.Economy;

namespace App.Application.Economy;

// Mirrors docs/tehnicki-dokument.md §6.8. Keep frontend/src/types/api.ts in sync.

public record RewardEventDto(RewardType Type, int Coins, int Xp, int Sp, DateTimeOffset At, Guid? ReferenceId);
