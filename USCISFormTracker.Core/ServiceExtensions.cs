using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using USCISFormTracker.Core.Data;
using USCISFormTracker.Core.PdfReaders;

namespace USCISFormTracker.Core;

public static class ServiceExtensions
{
    public static IServiceCollection AddFormTrackerServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configuration
        services.AddSingleton(configuration);

        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddConfiguration(configuration.GetSection("Logging"));
        });

        // Database
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<FormTrackerDbContext>(options =>
            options.UseSqlite(connectionString));

        // HttpClient
        services.AddHttpClient();

        // Repository
        services.AddScoped<IFormRepository, FormRepository>();

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

        // Main service
        services.AddScoped<FormMonitorService>();

        return services;
    }
}
