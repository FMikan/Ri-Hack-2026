using System.Net;
using System.Net.Http.Json;
using App.Api.Endpoints;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace App.Api.Tests;

public class MigrationTests(TestApp app) : ApiTestBase(app)
{
    [Fact]
    public async Task Migration_upgrades_a_starter_database()
    {
        var oldTables = await App.InDbAsync(db => db.Database
            .SqlQueryRaw<string>("""select tablename as "Value" from pg_tables where tablename in ('Notes', '__EFMigrationsHistory')""")
            .ToListAsync());

        Assert.Empty(oldTables);
    }
}

// Liveness needs no database: it runs no checks, so a database outage must not make it unhealthy.
public class HealthTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory
        .WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:Default", "Host=db.invalid;Database=unused");
            b.UseSetting("Auth:JwtSigningKey", TestApp.JwtSigningKey);
        })
        .CreateClient();

    [Fact]
    public async Task Liveness_is_healthy_without_a_database()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal("Healthy", body!.Status);
    }
}
