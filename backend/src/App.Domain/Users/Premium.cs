namespace App.Domain.Users;

public enum Plan { Junior, Senior }

public record PremiumPlan(string Interval, int PriceCents, int Days);

// The Senior developer subscription (docs/premium-pretplata.md): what it costs, what it multiplies and what a Junior
// developer gets a day. Every number the subscription uses lives here.
public static class Premium
{
    public const double RatingMultiplier = 1.2, XpMultiplier = 1.2;
    public const int DailyCoins = 25;
    public const int JuniorProfessorQuestionsPerDay = 2, JuniorProfessorLessonsPerDay = 1, JuniorProfessorQuizzesPerDay = 1;
    public static readonly PremiumPlan Monthly = new("monthly", 499, 30), Yearly = new("yearly", 3999, 365);
    public static readonly PremiumPlan[] Plans = [Monthly, Yearly];
    // One animated legendary cosmetic per slot (Seed/shop.json keys), given with the first payment and kept for good.
    public static readonly string[] WelcomePack = ["frame.event-loop", "name-color.gold-master", "banner.backbone", "card.backbone", "board.phosphor"];
    // The simulated payment declines a card number ending like this, the way Stripe's test cards do.
    public const string DeclinedCardSuffix = "0002";

    public static PremiumPlan? PlanOf(string interval) => Plans.FirstOrDefault(p => p.Interval == interval);

    // A gain grows by the multiplier (rounded half up); a loss or nothing stays as it is.
    public static int Boost(int amount, double multiplier) =>
        amount > 0 ? (int)Math.Round(amount * multiplier, MidpointRounding.AwayFromZero) : amount;
}
