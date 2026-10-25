using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using App.Application.Auth;
using App.Application.Content;
using App.Application.Lessons;
using App.Application.Matches;
using App.Application.Queue;
using App.Application.Seasons;
using App.Domain.Content;
using App.Domain.Seasons;
using App.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.PostgreSql;

// One database for the whole run, truncated before every test, so test classes must not run in parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace App.Api.Tests;

// One PostgreSQL container and one API host per run. The clock is fake and shared with the API (tokens, timers, "today").
public sealed class TestApp : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string JwtSigningKey = "test-only-signing-key-with-at-least-32-characters";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();

    // Today 10:00 UTC (midday in Europe/Zagreb), not the real time: the run moves the clock by hours in total, and tests
    // about "the same day" must not cross a local midnight just because the suite started late in the evening.
    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(DateTime.UtcNow.Date.AddHours(10), TimeSpan.Zero));

    public FakeEmailSender Emails { get; } = new();

    public FakeGoogleTokens Google { get; } = new();

    public FakeAi Ai { get; } = new();

    public JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Start from the starter's schema, as production does, so every run also proves the upgrade path.
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE "Notes" ("Id" uuid PRIMARY KEY, "Text" varchar(500) NOT NULL, "CreatedAt" timestamptz NOT NULL);
            CREATE TABLE "__EFMigrationsHistory" ("MigrationId" varchar(150) PRIMARY KEY, "ProductVersion" varchar(32) NOT NULL);
            INSERT INTO "__EFMigrationsHistory" VALUES ('20261024082121_InitialCreate', '10.0.13');
            """);
        await db.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());
        builder.UseSetting("Auth:JwtSigningKey", JwtSigningKey);
        builder.UseSetting("Game:TickEnabled", "false");
        builder.UseSetting("Database:SeedOnStartup", "false");
        builder.UseSetting("RateLimit:Enabled", "false");
        builder.UseSetting("Ai:TimeoutMs", "50");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(Clock);
            services.AddSingleton<IEmailSender>(Emails);
            services.AddSingleton<IGoogleTokenVerifier>(Google);
            services.AddSingleton<IExplanationGenerator>(Ai);
            services.AddSingleton<ILessonGenerator>(Ai);
        });
    }

    public async Task ResetAsync()
    {
        Emails.Sent.Clear();
        Google.Tokens.Clear();
        Ai.Reset();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tables = await db.Database
            .SqlQueryRaw<string>("select tablename as \"Value\" from pg_tables where schemaname = 'public' and tablename <> '__ef_migrations_history'")
            .ToListAsync();
        // Table names come from pg_tables, not from input; identifiers cannot be SQL parameters.
#pragma warning disable EF1002
        await db.Database.ExecuteSqlRawAsync($"truncate {string.Join(", ", tables.Select(t => $"\"{t}\""))} cascade");
#pragma warning restore EF1002
        // A season that started a month ago, so patched concepts pay normal SP unless a test moves the start.
        var now = Clock.GetUtcNow();
        db.Seasons.Add(new Season { Number = 1, StartsAt = now.AddDays(-30), EndsAt = now.AddDays(60) });
        await db.SaveChangesAsync();
    }

    public async Task<T> InDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    // What GameTickService does every second; tests call it after moving the clock.
    public async Task TickAsync()
    {
        List<Guid> due;
        using (var scope = Services.CreateScope())
            due = await scope.ServiceProvider.GetRequiredService<MatchService>().DueMatchIdsAsync(default);
        foreach (var matchId in due)
        {
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<MatchService>().AdvanceAsync(matchId, default);
        }
    }

    // What GameTickService does every 2 s for the ranked queue.
    public async Task MatchmakeAsync()
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<QueueService>().RunMatchmakingAsync(default);
    }

    // What GameTickService does every minute: grandmasters, the weekly and seasonal snapshots, titles, cleanup.
    public async Task HousekeepAsync()
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<HousekeepingService>().RunAsync(default);
    }

    public async Task AdvanceAsync(TimeSpan by)
    {
        Clock.Advance(by);
        await TickAsync();
    }
}

// Records reset e-mails instead of sending them; tests read the token from here.
public sealed class FakeEmailSender : IEmailSender
{
    public List<(string To, string Token)> Sent { get; } = [];

    public Task SendPasswordResetAsync(string to, string token, CancellationToken cancellationToken)
    {
        Sent.Add((to, token));
        return Task.CompletedTask;
    }
}

// Stands in for Google: an ID token is valid only if a test put it in Tokens.
public sealed class FakeGoogleTokens : IGoogleTokenVerifier
{
    public Dictionary<string, GoogleIdentity> Tokens { get; } = [];

    public string? ClientId => "test-client.apps.googleusercontent.com";

    public Task<GoogleIdentity?> VerifyAsync(string idToken, CancellationToken cancellationToken) => Task.FromResult(Tokens.GetValueOrDefault(idToken));
}

// Stands in for Claude: counts calls and can be slowed down past Ai:TimeoutMs (50 ms in tests) to force the fallback.
public sealed class FakeAi : IExplanationGenerator, ILessonGenerator
{
    public int ExplanationCalls;
    public int CardCalls;
    public TimeSpan Delay { get; set; }

    public void Reset() => (ExplanationCalls, CardCalls, Delay) = (0, 0, TimeSpan.Zero);

    public async Task<GeneratedExplanation> ExplainAsync(ExplanationInput input, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref ExplanationCalls);
        await Task.Delay(Delay, cancellationToken);
        return new GeneratedExplanation("AI why correct", "AI why distractor", "AI remember");
    }

    public async Task<GeneratedCard> CardAsync(LessonCardInput input, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref CardCalls);
        await Task.Delay(Delay, cancellationToken);
        return new GeneratedCard($"AI card: {input.Concept}", "AI why", "const x = 1;", "js", "AI misconception");
    }
}

[CollectionDefinition(nameof(ApiCollection))]
public class ApiCollection : ICollectionFixture<TestApp>;

public record Session(HttpClient Client, AuthResponse Auth);

[Collection(nameof(ApiCollection))]
public abstract class ApiTestBase(TestApp app) : IAsyncLifetime
{
    protected TestApp App { get; } = app;
    protected HttpClient Client { get; } = app.CreateClient();

    public Task InitializeAsync() => App.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    protected Task<T?> ReadAsync<T>(HttpResponseMessage response) => response.Content.ReadFromJsonAsync<T>(App.Json);

    protected Task<HttpResponseMessage> PostAsync<T>(HttpClient client, string path, T body) => client.PostAsJsonAsync(path, body, App.Json);

    // Registers a user and returns a client that sends its bearer token.
    protected async Task<Session> RegisterAsync(string username, string password = "secret-123")
    {
        var response = await PostAsync(Client, "/api/auth/register", new RegisterRequest { Email = $"{username}@example.com", Username = username, Password = password });
        response.EnsureSuccessStatusCode();
        var auth = (await ReadAsync<AuthResponse>(response))!;
        var client = App.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return new Session(client, auth);
    }

    // A fresh access token for the same account, e.g. after moving the clock past the token's 15 minutes.
    protected async Task<Session> LoginAgainAsync(Session session, string password = "secret-123")
    {
        var response = await PostAsync(Client, "/api/auth/login", new LoginRequest { Email = session.Auth.User.Email, Password = password });
        response.EnsureSuccessStatusCode();
        var auth = (await ReadAsync<AuthResponse>(response))!;
        var client = App.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return new Session(client, auth);
    }

    // topic-1..topic-n with two concepts each; sortOrder follows the given order.
    protected Task<Topic[]> SeedTopicsAsync(params int[] sortOrders) => App.InDbAsync(async db =>
    {
        var topics = sortOrders.Select((order, i) => new Topic
        {
            Slug = $"topic-{i + 1}",
            Name = $"Topic {i + 1}",
            Description = $"Description {i + 1}",
            SortOrder = order,
            Concepts =
            {
                new Concept { Slug = $"topic-{i + 1}.a", Name = "A", Description = "Concept A", SortOrder = 1 },
                new Concept { Slug = $"topic-{i + 1}.b", Name = "B", Description = "Concept B", SortOrder = 2 },
            },
        }).ToArray();
        db.Topics.AddRange(topics);
        await db.SaveChangesAsync();
        return topics;
    });
}
