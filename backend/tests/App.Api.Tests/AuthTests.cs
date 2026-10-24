using System.Net;
using System.Net.Http.Json;
using App.Application;
using App.Application.Auth;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Tests;

public class AuthTests(TestApp app) : ApiTestBase(app)
{
    [Fact]
    public async Task Register_returns_tokens_and_me()
    {
        var response = await PostAsync(Client, "/api/auth/register", new { email = "Ana@Example.com", username = "ana", password = "secret-123" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var auth = await ReadAsync<AuthResponse>(response);
        Assert.NotEmpty(auth!.AccessToken);
        Assert.NotEmpty(auth.RefreshToken);
        Assert.True(auth.AccessTokenExpiresAt > App.Clock.GetUtcNow());
        Assert.Equal("ana@example.com", auth.User.Email);
        Assert.Equal("ana", auth.User.Username);
    }

    [Fact]
    public async Task Register_with_taken_email_is_409()
    {
        await RegisterAsync("ana");

        var response = await PostAsync(Client, "/api/auth/register", new { email = "ANA@example.com", username = "ana2", password = "secret-123" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadAsync<ProblemDetails>(response);
        Assert.Equal(ErrorCodes.AuthEmailTaken, problem!.Type);
    }

    [Fact]
    public async Task Register_with_taken_username_case_insensitive_is_409()
    {
        await RegisterAsync("ana");

        var response = await PostAsync(Client, "/api/auth/register", new { email = "other@example.com", username = "Ana", password = "secret-123" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadAsync<ProblemDetails>(response);
        Assert.Equal(ErrorCodes.AuthUsernameTaken, problem!.Type);
    }

    [Fact]
    public async Task Register_with_short_password_is_400_validation()
    {
        var response = await PostAsync(Client, "/api/auth/register", new { email = "ana@example.com", username = "ana", password = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadAsync<ValidationProblemDetails>(response);
        Assert.Contains("Password", problem!.Errors.Keys);
    }

    [Fact]
    public async Task Register_with_blank_body_is_400()
    {
        var response = await PostAsync(Client, "/api/auth/register", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Malformed_json_is_400_not_500()
    {
        var response = await Client.PostAsync("/api/auth/register", new StringContent("{", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Login_returns_tokens()
    {
        await RegisterAsync("ana");

        var response = await PostAsync(Client, "/api/auth/login", new { email = "ana@example.com", password = "secret-123" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await ReadAsync<AuthResponse>(response);
        Assert.Equal("ana", auth!.User.Username);
        Assert.NotEmpty(auth.RefreshToken);
    }

    [Fact]
    public async Task Login_with_wrong_password_is_401_problem()
    {
        await RegisterAsync("ana");

        var wrongPassword = await PostAsync(Client, "/api/auth/login", new { email = "ana@example.com", password = "wrong-password" });
        var unknownEmail = await PostAsync(Client, "/api/auth/login", new { email = "nobody@example.com", password = "secret-123" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        var problem = await ReadAsync<ProblemDetails>(wrongPassword);
        Assert.Equal(ErrorCodes.AuthInvalidCredentials, problem!.Type);
    }

    [Fact]
    public async Task Protected_endpoint_without_token_is_401()
    {
        var response = await Client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Expired_access_token_is_401_problem()
    {
        var session = await RegisterAsync("ana");
        App.Clock.Advance(TimeSpan.FromMinutes(20));

        var response = await session.Client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_rotates_token_and_revokes_old()
    {
        var session = await RegisterAsync("ana");

        var refreshed = await PostAsync(Client, "/api/auth/refresh", new RefreshRequest { RefreshToken = session.Auth.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var next = await ReadAsync<AuthResponse>(refreshed);
        Assert.NotEqual(session.Auth.RefreshToken, next!.RefreshToken);

        var reused = await PostAsync(Client, "/api/auth/refresh", new RefreshRequest { RefreshToken = session.Auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
    }

    [Fact]
    public async Task Reused_revoked_refresh_token_is_401_and_revokes_family()
    {
        var session = await RegisterAsync("ana");
        var next = (await ReadAsync<AuthResponse>(await PostAsync(Client, "/api/auth/refresh", new RefreshRequest { RefreshToken = session.Auth.RefreshToken })))!;

        var reused = await PostAsync(Client, "/api/auth/refresh", new RefreshRequest { RefreshToken = session.Auth.RefreshToken });
        var latest = await PostAsync(Client, "/api/auth/refresh", new RefreshRequest { RefreshToken = next.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, latest.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_refresh_token()
    {
        var session = await RegisterAsync("ana");

        var logout = await PostAsync(session.Client, "/api/auth/logout", new RefreshRequest { RefreshToken = session.Auth.RefreshToken });
        var refresh = await PostAsync(Client, "/api/auth/refresh", new RefreshRequest { RefreshToken = session.Auth.RefreshToken });

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }
}
