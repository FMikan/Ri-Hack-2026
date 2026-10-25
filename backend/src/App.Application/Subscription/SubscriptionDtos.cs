using System.ComponentModel.DataAnnotations;
using App.Domain.Users;

namespace App.Application.Subscription;

// Mirrors docs/tehnicki-dokument.md §6.13. Keep frontend/src/types/api.ts in sync.

public record PremiumPlanDto(string Interval, int PriceCents, int Days);

// Today's AI Professor use; a limit is null for a Senior developer (no limit).
public record ProfessorUsageDto(int QuestionsUsed, int? QuestionsLimit, int LessonsUsed, int? LessonsLimit, int QuizzesUsed, int? QuizzesLimit);

public record SubscriptionDto(
    Plan Plan, DateTimeOffset? PremiumUntil, string? Interval, bool AutoRenew, string? CardLast4,
    int DailyCoins, bool DailyCoinsClaimed, PremiumPlanDto[] Plans, ProfessorUsageDto Professor);

// A simulated card payment (hackathon): nothing is charged; a number ending in 0002 is declined like Stripe's test card.
public record SubscribeRequest
{
    [Required]
    [RegularExpression("^(monthly|yearly)$", ErrorMessage = "Interval must be 'monthly' or 'yearly'.")]
    public required string Interval { get; init; }

    [Required]
    [StringLength(80, MinimumLength = 2)]
    public required string CardHolder { get; init; }

    [Required]
    [RegularExpression("^[0-9 ]{13,23}$", ErrorMessage = "Card number must be 13 to 19 digits.")]
    public required string CardNumber { get; init; }

    [Required]
    [RegularExpression("^(0[1-9]|1[0-2])/[0-9]{2}$", ErrorMessage = "Expiry must be MM/YY.")]
    public required string CardExpiry { get; init; }

    [Required]
    [RegularExpression("^[0-9]{3,4}$", ErrorMessage = "CVC must be 3 or 4 digits.")]
    public required string CardCvc { get; init; }
}
