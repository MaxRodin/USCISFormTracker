using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using USCISFormTracker.Core;
using USCISFormTracker.Processor;
using USCISFormTracker.Processor.Data;
using DotNetEnv;

// Load environment variables from .env file
Env.Load();

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var services = new ServiceCollection();

// Add logging
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.AddConfiguration(configuration.GetSection("Logging"));
});

// Add Core and Processor services
services.AddFormTrackerCoreServices(configuration);
services.AddProcessorServices(configuration);

// Add HttpClient for Core usage
services.AddHttpClient();

var serviceProvider = services.BuildServiceProvider();

// Ensure database is created
using (var scope = serviceProvider.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<FormTrackerDbContext>();
    dbContext.Database.EnsureCreated();
    Console.WriteLine("Database initialized");
}

// Run the monitor service
Console.WriteLine("Starting USCIS Form Tracker...");
Console.WriteLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");

try
{
    using var scope = serviceProvider.CreateScope();

    // Get services
    var repository = scope.ServiceProvider.GetRequiredService<IFormRepository>();
    var comparisonService = scope.ServiceProvider.GetRequiredService<IFormComparisonService>();
    var httpClient = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // Get existing forms
    var existingRecords = await repository.GetAllFormRecordsAsync();
    logger.LogInformation("Found {Count} existing forms", existingRecords.Count);

    // Convert to snapshots
    var snapshots = existingRecords.Select(r => new USCISFormTracker.Core.Models.FormSnapshot
    {
        FileName = r.FileName,
        FullLink = r.FullLink,
        FormName = r.FormName,
        Hash = r.Hash,
        ExtractedText = r.ExtractedText
    }).ToList();

    // Run comparison
    var summary = await comparisonService.CompareFormsAsync(snapshots, httpClient);

    Console.WriteLine($"Added: {summary.AddedForms.Count}, Changed: {summary.ChangedForms.Count}, Deleted: {summary.DeletedForms.Count}");
    Console.WriteLine("Form monitoring completed successfully");
}
catch (Exception ex)
{
    Console.WriteLine($"Error during form monitoring: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
    return 1;
}

return 0;
