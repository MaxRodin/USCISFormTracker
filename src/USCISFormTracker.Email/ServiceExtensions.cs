using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using USCISFormTracker.Formatting;

namespace USCISFormTracker.Email;

/// <summary>
/// Email services - Mailgun sender and run-summary notifier
/// </summary>
public static class ServiceExtensions
{
    public static IServiceCollection AddEmailServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Mailgun settings come from MAILGUN_* keys (environment variables or .env),
        // mirroring how the data layer reads DATABASE_*
        var options = new MailgunOptions
        {
            ApiKey = Require(configuration, "MAILGUN_API_KEY"),
            Domain = Require(configuration, "MAILGUN_DOMAIN"),
            FromEmail = Require(configuration, "MAILGUN_FROM_EMAIL"),
            FromName = configuration["MAILGUN_FROM_NAME"] ?? "USCIS Form Tracker",
            MailingListAddress = Require(configuration, "MAILGUN_MAILING_LIST_ADDRESS"),
        };

        services.AddSingleton(options);
        services.AddSingleton<IEmailSender, MailgunEmailSender>();
        services.AddSingleton<IRunSummaryFormatter, RunSummaryFormatter>();
        services.AddSingleton<IRunSummaryNotifier, RunSummaryNotifier>();

        return services;
    }

    private static string Require(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{key} not configured")
            : value;
    }
}
