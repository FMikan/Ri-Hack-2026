using App.Application.Economy;
using App.Application.Me;
using App.Application.Professor;
using App.Domain;
using App.Domain.Economy;
using App.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace App.Application.Subscription;

// The Senior developer subscription (docs/premium-pretplata.md). Payment is simulated for the hackathon: the card is
// never charged and only its last four digits are kept. Production swaps the "charge" in SubscribeAsync for Stripe
// Checkout and renews from its webhook; everything else stays.
public class SubscriptionService(IAppDbContext db, Rewarder rewarder, MeService me, ProfessorService professor, TimeProvider time)
{
    public async Task<SubscriptionDto> GetAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw Gone();
        var now = time.GetUtcNow();
        var claimed = await db.RewardEvents.AnyAsync(e => e.DedupeKey == DailyKey(userId, now), ct);
        return new SubscriptionDto(
            user.PlanAt(now), user.PremiumUntil, user.PremiumInterval, user.PremiumAutoRenew, user.PremiumCardLast4,
            Premium.DailyCoins, claimed, [.. Premium.Plans.Select(p => new PremiumPlanDto(p.Interval, p.PriceCents, p.Days))],
            await professor.UsageAsync(userId, ct));
    }

    public async Task<SubscriptionDto> SubscribeAsync(Guid userId, SubscribeRequest request, CancellationToken ct)
    {
        var plan = Premium.PlanOf(request.Interval)!; // validated by the request
        var digits = request.CardNumber.Replace(" ", "");
        if (digits.EndsWith(Premium.DeclinedCardSuffix))
            throw new AppException(ErrorCodes.PaymentDeclined, "The card was declined. Try another card.", 402);
        var now = time.GetUtcNow();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw Gone();
        // Paying while still a Senior adds the time on top of what is left.
        var from = user.PremiumUntil > now ? user.PremiumUntil.Value : now;
        (user.PremiumUntil, user.PremiumInterval, user.PremiumAutoRenew, user.PremiumCardLast4) = (from.AddDays(plan.Days), plan.Interval, true, digits[^4..]);
        // The welcome pack: one animated cosmetic per slot, kept for good; a returning subscriber already owns it.
        foreach (var itemId in await db.ShopItems.Where(i => Premium.WelcomePack.Contains(i.Key)).Select(i => i.Id).ToListAsync(ct))
            await db.Database.ExecuteSqlAsync(
                $"insert into user_items (user_id, item_id, acquired_at) values ({userId}, {itemId}, {now}) on conflict do nothing", ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(userId, ct);
    }

    // Stops the renewal; Senior stays until premiumUntil, then Junior.
    public async Task<SubscriptionDto> CancelAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw Gone();
        if (!user.IsSenior(time.GetUtcNow()))
            throw new AppException(ErrorCodes.SubscriptionNotActive, "You are not a Senior developer right now.");
        user.PremiumAutoRenew = false;
        await db.SaveChangesAsync(ct);
        return await GetAsync(userId, ct);
    }

    // 25 coins once a day (Europe/Zagreb) for a Senior developer, through the ledger; a second claim pays nothing.
    public async Task<MeDto> ClaimDailyCoinsAsync(Guid userId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        if (!await db.Users.AnyAsync(u => u.Id == userId && u.PremiumUntil > now, ct))
            throw new AppException(ErrorCodes.SubscriptionSeniorOnly, "The daily coins are for Senior developers.");
        await rewarder.GrantAsync(userId, new Grant(RewardType.PremiumDaily, Premium.DailyCoins, 0, 0, null, DailyKey(userId, now)), ct);
        return await me.GetAsync(userId, ct);
    }

    private static string DailyKey(Guid userId, DateTimeOffset now) => $"premium_daily:{Clock.Today(now):yyyy-MM-dd}:{userId}";

    private static AppException Gone() => new(ErrorCodes.AuthRefreshInvalid, "The account no longer exists.", 401);
}
