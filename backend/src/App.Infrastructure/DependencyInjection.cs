using App.Application;
using App.Application.Auth;
using App.Application.Content;
using App.Application.Lessons;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace App.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' is missing. Copy .env.example to .env in the repository root, " +
                "or set the ConnectionStrings__Default environment variable.");

        // The snake_case convention also renames the history table's columns, so it gets its own snake_case name:
        // databases from before the convention keep an old "__EFMigrationsHistory" ("MigrationId") that EF could not read.
        services.AddDbContext<AppDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention());
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<Seed.Seeder>();
        services.AddSingleton<IEmailSender, EmailSender>();
        services.AddSingleton<ClaudeAi>();
        services.AddSingleton<IExplanationGenerator>(sp => sp.GetRequiredService<ClaudeAi>());
        services.AddSingleton<ILessonGenerator>(sp => sp.GetRequiredService<ClaudeAi>());

        // Tagged "ready" so only the readiness endpoint runs it; liveness never touches the database.
        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

        return services;
    }
}
