using MassTransit;
using USCISFormTracker.Emailer;
using USCISFormTracker.Emailer.Consumers;

var builder = Host.CreateApplicationBuilder(args);

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
    // Register consumers
    x.AddConsumer<FormChangeDetectedConsumer>();
    x.AddConsumer<FormAddedConsumer>();
    x.AddConsumer<FormDeletedConsumer>();

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
host.Run();
