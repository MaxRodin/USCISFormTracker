using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using USCISFormTracker.Data;

namespace USCISFormTracker.Processor;

/// <summary>
/// Processor services - orchestration layer
/// </summary>
public static class ServiceExtensions
{
    public static IServiceCollection AddProcessorServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Data layer (DbContext + Repository)
        services.AddDataServices(configuration);

        return services;
    }
}
