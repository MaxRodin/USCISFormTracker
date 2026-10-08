using MassTransit;
using USCISFormTracker.Emailer;
using USCISFormTracker.Emailer.Consumers;
using USCISFormTracker.Emailer.Services;
using USCISFormTracker.Formatting;

var builder = Host.CreateApplicationBuilder(args);

// Register Formatting Services
builder.Services.AddSingleton<IRunSummaryFormatter, RunSummaryFormatter>();

// Register Email Content Builder
builder.Services.AddSingleton<IEmailContentBuilder, EmailContentBuilder>();

// Register Email Sender
builder.Services.AddSingleton<IEmailSender>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var apiKey = configuration["Mailgun:ApiKey"]
        ?? throw new InvalidOperationException("Mailgun:ApiKey not configured");
    var domain = configuration["Mailgun:Domain"]
        ?? throw new InvalidOperationException("Mailgun:Domain not configured");
    var fromEmail = configuration["Mailgun:FromEmail"]
        ?? throw new InvalidOperationException("Mailgun:FromEmail not configured");
    var fromName = configuration["Mailgun:FromName"]
        ?? throw new InvalidOperationException("Mailgun:FromName not configured");
    var mailingListAddress = configuration["Mailgun:MailingListAddress"]; // Optional

    return new MailgunEmailSender(apiKey, domain, fromEmail, fromName, mailingListAddress);
});

// Configure MassTransit with RabbitMQ
builder.Services.AddMassTransit(x =>
{
    // Register consumer (only RunSummaryConsumer - we always send aggregate summaries)
    x.AddConsumer<RunSummaryConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var configuration = context.GetRequiredService<IConfiguration>();
        var host = configuration["RabbitMQ:Host"] ?? "localhost";
        var username = configuration["RabbitMQ:Username"] ?? "guest";
        var password = configuration["RabbitMQ:Password"] ?? "guest";

        cfg.Host(host, h =>
        {
            h.Username(username);
            h.Password(password);
        });

        cfg.ConfigureEndpoints(context);
    });
});

var host = builder.Build();

// Validate required configuration before starting
ValidateConfiguration(builder.Configuration);

host.Run();

static void ValidateConfiguration(IConfiguration configuration)
{
    var requiredConfigs = new Dictionary<string, string?>
    {
        ["Mailgun:ApiKey"] = configuration["Mailgun:ApiKey"],
        ["Mailgun:Domain"] = configuration["Mailgun:Domain"],
        ["Mailgun:FromEmail"] = configuration["Mailgun:FromEmail"],
        ["Mailgun:FromName"] = configuration["Mailgun:FromName"],
        ["Mailgun:MailingListAddress"] = configuration["Mailgun:MailingListAddress"],
        ["RabbitMQ:Host"] = configuration["RabbitMQ:Host"],
    };

    var missingConfigs = requiredConfigs
        .Where(kvp => string.IsNullOrWhiteSpace(kvp.Value))
        .Select(kvp => kvp.Key)
        .ToList();

    if (missingConfigs.Any())
    {
        var errorMessage = $"Missing required configuration: {string.Join(", ", missingConfigs)}. " +
                          "Please check your appsettings.json file.";
        Console.Error.WriteLine($"ERROR: {errorMessage}");
        throw new InvalidOperationException(errorMessage);
    }

    Console.WriteLine("✓ Configuration validation passed");
}
