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

// Configuration
services.AddSingleton<IConfiguration>(configuration);

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
services.AddScoped<IPdfReader, PdfPigReader>();
services.AddScoped<IDiffer, TextDiffer>();

// Web PDF Getter
services.AddScoped<IWebPdfGetter>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    var formsPageUrl = configuration["UscisConfig:FormsPageUrl"]
        ?? throw new InvalidOperationException("UscisConfig:FormsPageUrl not configured");
    return new UscisWebPdfGetter(httpClient, formsPageUrl);
});

// Email services
services.AddScoped<IEmailSender>(sp =>
{
    var apiKey = configuration["Mailgun:ApiKey"]
        ?? throw new InvalidOperationException("Mailgun:ApiKey not configured");
    var domain = configuration["Mailgun:Domain"]
        ?? throw new InvalidOperationException("Mailgun:Domain not configured");
    var fromEmail = configuration["Mailgun:FromEmail"]
        ?? throw new InvalidOperationException("Mailgun:FromEmail not configured");
    var fromName = configuration["Mailgun:FromName"]
        ?? throw new InvalidOperationException("Mailgun:FromName not configured");

    return new MailgunEmailSender(apiKey, domain, fromEmail, fromName);
});

services.AddScoped<IEmailService>(sp =>
{
    var emailSender = sp.GetRequiredService<IEmailSender>();
    var toEmails = configuration.GetSection("EmailNotifications:ToEmails").Get<List<string>>()
        ?? throw new InvalidOperationException("EmailNotifications:ToEmails not configured");
    var subjectTemplate = configuration["EmailNotifications:Subject"]
        ?? throw new InvalidOperationException("EmailNotifications:Subject not configured");

    return new EmailService(emailSender, toEmails, subjectTemplate);
});

// Main service
services.AddScoped<FormMonitorService>();

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
