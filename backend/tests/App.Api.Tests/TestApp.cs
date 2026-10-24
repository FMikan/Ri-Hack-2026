using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using App.Application.Auth;
using App.Application.Matches;
using App.Domain.Content;
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

    public FakeTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    public FakeEmailSender Emails { get; } = new();

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
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(Clock);
            services.AddSingleton<IEmailSender>(Emails);
        });
    }

    public async Task ResetAsync()
    {
        Emails.Sent.Clear();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tables = await db.Database
            .SqlQueryRaw<string>("select tablename as \"Value\" from pg_tables where schemaname = 'public' and tablename <> '__ef_migrations_history'")
            .ToListAsync();
        // Table names come from pg_tables, not from input; identifiers cannot be SQL parameters.
#pragma warning disable EF1002
        await db.Database.ExecuteSqlRawAsync($"truncate {string.Join(", ", tables.Select(t => $"\"{t}\""))} cascade");
#pragma warning restore EF1002
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
