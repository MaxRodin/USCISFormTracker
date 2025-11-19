using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using USCISFormTracker.Core;

namespace USCISFormTracker.Data;

/// <summary>
/// Data layer services - database context and repository
/// </summary>
public static class ServiceExtensions
{
    public static IServiceCollection AddDataServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Get database connection parameters from configuration
        var dbHost = configuration["DATABASE_HOST"] ?? throw new InvalidOperationException("DATABASE_HOST not configured");
        var dbPort = configuration["DATABASE_PORT"] ?? "5432";
        var dbName = configuration["DATABASE_NAME"] ?? throw new InvalidOperationException("DATABASE_NAME not configured");
        var dbUser = configuration["DATABASE_USER"] ?? throw new InvalidOperationException("DATABASE_USER not configured");
        var dbPassword = configuration["DATABASE_PASSWORD"] ?? throw new InvalidOperationException("DATABASE_PASSWORD not configured");

        var connectionString = $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPassword}";

        // Register DbContext
        services.AddDbContext<FormTrackerDbContext>(options =>
            options.UseNpgsql(connectionString));

        // Register Repository (implements IFormRepository from Core)
        services.AddScoped<IFormRepository, FormRepository>();

        return services;
    }
}
