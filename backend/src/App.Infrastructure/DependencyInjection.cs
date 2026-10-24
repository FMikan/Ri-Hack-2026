using App.Application;
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

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // Tagged "ready" so only the readiness endpoint runs it; liveness never touches the database.
        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

        return services;
    }
}
