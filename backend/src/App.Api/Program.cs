using App.Api.Endpoints;
using App.Application.Notes;
using App.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

LoadDotEnv();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
// Malformed bodies become 400 Problem Details instead of exceptions (which would surface as 500 in Development).
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);
builder.Services.AddValidation();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<NoteService>();

var app = builder.Build();

// Production (single instance on the VPS) applies pending migrations at startup. Locally, run `dotnet ef database update`.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapHealthEndpoints();
app.MapNoteEndpoints();

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
