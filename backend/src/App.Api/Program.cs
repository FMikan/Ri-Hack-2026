using System.Text.Json;
using System.Text.Json.Serialization;
using App.Api;
using App.Api.Endpoints;
using App.Application;
using App.Application.Auth;
using App.Application.Content;
using App.Application.Lessons;
using App.Application.Matches;
using App.Application.Me;
using App.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

LoadDotEnv();

var builder = WebApplication.CreateBuilder(args);

// Declares the bearer token so Scalar shows an Authorize field and sends it on every request.
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Components ??= new OpenApiComponents();
    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
    document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "accessToken from POST /api/auth/register or /api/auth/login",
    };
    document.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] }];
    return Task.CompletedTask;
}));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AppExceptionHandler>();
// Enums travel as snake_case strings ("true_false", "in_progress"), the same spelling the database stores.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)));
// Malformed bodies become 400 Problem Details instead of exceptions (which would surface as 500 in Development).
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);
builder.Services.AddValidation();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAppAuth(builder.Configuration);
builder.Services.AddAppRateLimits();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<MeService>();
builder.Services.AddScoped<ContentService>();
builder.Services.AddScoped<MatchService>();
builder.Services.AddScoped<LessonService>();
// Live AI calls (explanations, lesson cards) give up after this and fall back (idea §16: 8 s).
builder.Services.AddSingleton(new AiOptions(TimeSpan.FromMilliseconds(builder.Configuration.GetValue("Ai:TimeoutMs", 8000))));
// Tests turn the tick off and advance matches themselves with a fake clock.
if (builder.Configuration.GetValue("Game:TickEnabled", true)) builder.Services.AddHostedService<GameTickService>();

var app = builder.Build();

// Production sits behind the host's Caddy and the web container's Caddy. X-Forwarded-* is trusted only from
// private (Docker) and loopback addresses, walking back to the first public one, so rate limits see the player's IP.
var forwarded = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, ForwardLimit = null };
foreach (var network in new[] { "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16" }) forwarded.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
app.UseForwardedHeaders(forwarded);

// Production (single instance on the VPS) applies pending migrations at startup. Locally, run `dotnet ef database update`.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

// Topics, concepts and questions from App.Infrastructure/Seed (Development and production; tests seed their own data).
if (app.Configuration.GetValue<bool>("Database:SeedOnStartup"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<App.Infrastructure.Seed.Seeder>().SeedAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
// Tests switch it off (one test switches it back on); everywhere else it is on.
if (app.Configuration.GetValue("RateLimit:Enabled", true)) app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapMeEndpoints();
app.MapContentEndpoints();
app.MapMatchEndpoints();
app.MapLessonEndpoints();

app.Run();

// Local dev convenience: load the repository-root .env (shared with docker compose) by walking up
// from the working directory, stopping at the repository root. Variables already set in the real environment win.
static void LoadDotEnv()
{
    for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
    {
        var path = Path.Combine(dir.FullName, ".env");
        if (!File.Exists(path))
        {
            if (Path.Exists(Path.Combine(dir.FullName, ".git"))) return;
            continue;
        }

        foreach (var line in File.ReadLines(path))
        {
            var separator = line.IndexOf('=');
            if (separator <= 0 || line.TrimStart().StartsWith('#')) continue;

            var key = line[..separator].Trim();
            Environment.SetEnvironmentVariable(key, Environment.GetEnvironmentVariable(key) ?? line[(separator + 1)..].Trim().Trim('"'));
        }
        return;
    }
}
