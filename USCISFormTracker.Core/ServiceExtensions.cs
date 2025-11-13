using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using USCISFormTracker.Core.PdfReaders;

namespace USCISFormTracker.Core;

/// <summary>
/// Core services - pure business logic, no database or messaging dependencies
/// </summary>
public static class ServiceExtensions
{
    public static IServiceCollection AddFormTrackerCoreServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // HttpClient
        services.AddHttpClient();

        // Core services
        services.AddScoped<IHasher, Sha256Hasher>();
        services.AddScoped<IPdfReader, PdfPigLayoutPdfReader>(); // Using PdfPig's RecursiveXYCut algorithm
        services.AddScoped<IDiffer, DiffPlexDiffer>();

        // Web PDF Getter
        services.AddScoped<IWebPdfGetter>(sp =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
            var formsPageUrl = configuration["UscisConfig:FormsPageUrl"]
                ?? throw new InvalidOperationException("UscisConfig:FormsPageUrl not configured");
            return new UscisWebPdfGetter(httpClient, formsPageUrl);
        });

        // Form comparison service (pure logic)
        services.AddScoped<IFormComparisonService, FormComparisonService>();

        return services;
    }
}
