using System.Net;
using System.Net.Http.Json;
using App.Api.Endpoints;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace App.Api.Tests;

// These tests need no running database: liveness runs no checks and validation rejects before any query.
public class ApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory
        .WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:Default", "Host=db.invalid;Database=unused"))
        .CreateClient();

    [Fact]
    public async Task Liveness_is_healthy_without_a_database()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal("Healthy", body!.Status);
    }

    [Fact]
    public async Task Creating_a_blank_note_returns_validation_problem_details()
    {
        var response = await _client.PostAsJsonAsync("/api/notes", new { text = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("Text", problem!.Errors.Keys);
    }

    [Fact]
    public async Task Malformed_json_returns_400_not_500()
    {
        var response = await _client.PostAsync("/api/notes", new StringContent("{", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
