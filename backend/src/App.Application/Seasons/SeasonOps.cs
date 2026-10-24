using App.Domain.Seasons;
using Microsoft.EntityFrameworkCore;

namespace App.Application.Seasons;

public static class SeasonOps
{
    // The season covering `now`, created when it does not exist yet (the first one starts today; the next one where the
    // last ended). Two callers at once insert the same number, and "on conflict" keeps the first.
    public static async Task<Season> CurrentSeasonAsync(this IAppDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        while (true)
        {
            var latest = await db.Seasons.AsNoTracking().OrderByDescending(s => s.Number).FirstOrDefaultAsync(ct);
            if (latest is not null && latest.EndsAt > now) return latest;
            var next = latest?.Next() ?? Season.First(now);
            await db.Database.ExecuteSqlAsync(
                $"insert into seasons (id, number, starts_at, ends_at) values ({next.Id}, {next.Number}, {next.StartsAt}, {next.EndsAt}) on conflict (number) do nothing", ct);
        }
    }

    // A season row for the player, starting from the tier they have now (best tier of the season so far).
    public static Task JoinSeasonAsync(this IAppDbContext db, Guid userId, Guid seasonId, CancellationToken ct) =>
        db.Database.ExecuteSqlAsync(
            $"insert into user_seasons (user_id, season_id, sp, best_tier) select id, {seasonId}, 0, tier from users where id = {userId} on conflict do nothing", ct);

    // Awards a title, created on first use. The weekly title counts up every time; any other is (re)activated once.
    public static async Task GrantTitleAsync(this IAppDbContext db, Guid userId, (string Key, string Name) title, TitleKind kind, DateTimeOffset now, CancellationToken ct)
    {
        var counts = kind == TitleKind.Weekly ? 1 : 0;
        await db.Database.ExecuteSqlAsync(
            $"insert into titles (id, key, kind, name) values ({Guid.CreateVersion7()}, {title.Key}, {kind.ToString().ToLowerInvariant()}, {title.Name}) on conflict (key) do nothing", ct);
        await db.Database.ExecuteSqlAsync(
            $"""
            insert into user_titles (user_id, title_id, earned_at, count, active)
            select {userId}, id, {now}, 1, true from titles where key = {title.Key}
            on conflict (user_id, title_id) do update set count = user_titles.count + {counts}, active = true
            where {counts} = 1 or not user_titles.active
            """, ct);
    }
}
