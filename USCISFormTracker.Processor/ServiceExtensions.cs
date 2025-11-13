using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using USCISFormTracker.Processor.Data;

namespace USCISFormTracker.Processor;

/// <summary>
/// Processor services - database and orchestration layer
/// </summary>
public static class ServiceExtensions
{
    public static IServiceCollection AddProcessorServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        var dbHost = configuration["DATABASE_HOST"] ?? throw new InvalidOperationException("DATABASE_HOST not configured");
        var dbPort = configuration["DATABASE_PORT"] ?? "5432";
        var dbName = configuration["DATABASE_NAME"] ?? throw new InvalidOperationException("DATABASE_NAME not configured");
        var dbUser = configuration["DATABASE_USER"] ?? throw new InvalidOperationException("DATABASE_USER not configured");
        var dbPassword = configuration["DATABASE_PASSWORD"] ?? throw new InvalidOperationException("DATABASE_PASSWORD not configured");

        var connectionString = $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPassword}";

        services.AddDbContext<FormTrackerDbContext>(options =>
            options.UseNpgsql(connectionString));

        // Repository
        services.AddScoped<IFormRepository, FormRepository>();

        return services;
    }
}
