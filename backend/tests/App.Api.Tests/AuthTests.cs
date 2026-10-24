using System.Net;
using System.Net.Http.Json;
using App.Domain;
using App.Application.Auth;
using Microsoft.AspNetCore.Hosting;
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

    [Fact]
    public async Task Forgot_password_always_202_and_sends_email_only_for_existing_user()
    {
        await RegisterAsync("ana");

        var known = await PostAsync(Client, "/api/auth/forgot-password", new ForgotPasswordRequest { Email = "ANA@example.com" });
        var unknown = await PostAsync(Client, "/api/auth/forgot-password", new ForgotPasswordRequest { Email = "nobody@example.com" });

        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        Assert.Equal("ana@example.com", Assert.Single(App.Emails.Sent).To);
    }

    [Fact]
    public async Task Reset_password_with_valid_token_changes_password_and_revokes_sessions()
    {
        var session = await RegisterAsync("ana");
        var token = await ForgotAsync("ana@example.com");

        var reset = await ResetAsync(token, "new-secret-456");

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync("ana@example.com", "secret-123")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync("ana@example.com", "new-secret-456")).StatusCode);
        var refresh = await PostAsync(Client, "/api/auth/refresh", new RefreshRequest { RefreshToken = session.Auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Reset_token_is_single_use_and_expires_after_1h()
    {
        await RegisterAsync("ana");
        var older = await ForgotAsync("ana@example.com");
        var token = await ForgotAsync("ana@example.com");

        Assert.Equal(HttpStatusCode.NoContent, (await ResetAsync(token, "new-secret-456")).StatusCode);
        await AssertResetInvalidAsync(token);
        await AssertResetInvalidAsync(older); // a reset also spends every link sent before it

        var late = await ForgotAsync("ana@example.com");
        App.Clock.Advance(TimeSpan.FromHours(1));
        await AssertResetInvalidAsync(late);
    }

    [Fact]
    public async Task Reset_with_short_password_is_400_validation()
    {
        await RegisterAsync("ana");
        var token = await ForgotAsync("ana@example.com");

        var response = await ResetAsync(token, "short");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ResetAsync(token, "new-secret-456")).StatusCode);
    }

    [Fact]
    public async Task Login_is_rate_limited()
    {
        await using var limited = App.WithWebHostBuilder(builder => builder.UseSetting("RateLimit:Enabled", "true"));
        var client = limited.CreateClient();

        // 10 per minute: 21 attempts cannot all pass even if a window boundary falls between them.
        var responses = new List<HttpResponseMessage>();
        for (var i = 0; i < 21; i++)
            responses.Add(await PostAsync(client, "/api/auth/login", new LoginRequest { Email = "ana@example.com", Password = "wrong-password" }));

        var rejected = responses.First(r => r.StatusCode == HttpStatusCode.TooManyRequests);
        Assert.NotNull(rejected.Headers.RetryAfter?.Delta);
        Assert.Equal(429, (await ReadAsync<ProblemDetails>(rejected))!.Status);
        Assert.All(responses.Take(10), r => Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode));
    }

    private async Task<string> ForgotAsync(string email)
    {
        var sent = App.Emails.Sent.Count;
        (await PostAsync(Client, "/api/auth/forgot-password", new ForgotPasswordRequest { Email = email })).EnsureSuccessStatusCode();
        Assert.Equal(sent + 1, App.Emails.Sent.Count);
        return App.Emails.Sent[^1].Token;
    }

    private Task<HttpResponseMessage> ResetAsync(string token, string newPassword) =>
        PostAsync(Client, "/api/auth/reset-password", new ResetPasswordRequest { Token = token, NewPassword = newPassword });

    private Task<HttpResponseMessage> LoginAsync(string email, string password) =>
        PostAsync(Client, "/api/auth/login", new LoginRequest { Email = email, Password = password });

    private async Task AssertResetInvalidAsync(string token)
    {
        var response = await ResetAsync(token, "another-secret-789");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.AuthResetTokenInvalid, (await ReadAsync<ProblemDetails>(response))!.Type);
    }
}
