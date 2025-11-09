using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using USCISFormTracker.Core;
using USCISFormTracker.Core.Data;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var services = new ServiceCollection();
services.AddFormTrackerServices(configuration);

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
    var monitorService = scope.ServiceProvider.GetRequiredService<FormMonitorService>();
    await monitorService.MonitorFormsAsync();
    Console.WriteLine("Form monitoring completed successfully");
}
catch (Exception ex)
{
    Console.WriteLine($"Error during form monitoring: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
    return 1;
}

return 0;
