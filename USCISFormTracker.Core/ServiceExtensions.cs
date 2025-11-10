using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using USCISFormTracker.Core.Data;

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
        services.AddScoped<IPdfReader, ImprovedPdfPigReader>();
        services.AddScoped<IDiffer, DiffPlexDiffer>();

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

        return services;
    }
}
