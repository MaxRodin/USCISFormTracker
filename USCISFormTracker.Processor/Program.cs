using DotNetEnv;
using MassTransit;
using Quartz;
using USCISFormTracker.Core;
using USCISFormTracker.Processor;
using USCISFormTracker.Processor.Jobs;
using USCISFormTracker.Dto;

// Load environment variables from .env file (searching parent directories,
// since `dotnet run` sets the working directory to the project directory)
Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen on port 5000
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000);
});

// Add Core services (pure business logic)
builder.Services.AddFormTrackerCoreServices(builder.Configuration);

// Add Processor services (database, repositories, orchestration)
builder.Services.AddProcessorServices(builder.Configuration);

// Configure Quartz
builder.Services.AddQuartz(q =>
{
    // Use a unique job key
    var jobKey = new JobKey("FormMonitorJob");

    // Register the job
    q.AddJob<FormMonitorJob>(opts => opts.WithIdentity(jobKey));

    // Create a trigger for the job
    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("FormMonitorJob-trigger")
        // Run every day at 2:00 AM
        .WithCronSchedule(builder.Configuration["Quartz:CronSchedule"] ?? "0 0 2 * * ?")
        .WithDescription("Runs form monitoring daily at 2:00 AM"));
});

// Add Quartz hosted service
builder.Services.AddQuartzHostedService(options =>
{
    // Wait for jobs to complete on shutdown
    options.WaitForJobsToComplete = true;
});

// Configure MassTransit with RabbitMQ for publishing change events
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        var host = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
        var username = builder.Configuration["RabbitMQ:Username"] ?? "guest";
        var password = builder.Configuration["RabbitMQ:Password"] ?? "guest";

        cfg.Host(host, h =>
        {
            h.Username(username);
            h.Password(password);
        });

        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();

// Validate required configuration before starting
ValidateConfiguration();

// Add API endpoint to trigger job manually
app.MapPost("/api/run-job", async (ISchedulerFactory schedulerFactory) =>
{
    var scheduler = await schedulerFactory.GetScheduler();
    var jobKey = new JobKey("FormMonitorJob");

    // Trigger the job immediately
    await scheduler.TriggerJob(jobKey);

    return Results.Ok(new
    {
        message = "Form monitoring job triggered successfully",
        triggeredAt = DateTime.UtcNow
    });
});

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();

static void ValidateConfiguration()
{
    var requiredConfigs = new Dictionary<string, string?>
    {
        ["DATABASE_HOST"] = Environment.GetEnvironmentVariable("DATABASE_HOST"),
        ["DATABASE_PASSWORD"] = Environment.GetEnvironmentVariable("DATABASE_PASSWORD"),
        ["DATABASE_NAME"] = Environment.GetEnvironmentVariable("DATABASE_NAME"),
        ["DATABASE_USER"] = Environment.GetEnvironmentVariable("DATABASE_USER"),
    };

    var missingConfigs = requiredConfigs
        .Where(kvp => string.IsNullOrWhiteSpace(kvp.Value))
        .Select(kvp => kvp.Key)
        .ToList();

    if (missingConfigs.Any())
    {
        var errorMessage = $"Missing required configuration: {string.Join(", ", missingConfigs)}. " +
                          "Please check your .env file and environment variables.";
        Console.Error.WriteLine($"ERROR: {errorMessage}");
        throw new InvalidOperationException(errorMessage);
    }

    Console.WriteLine("✓ Configuration validation passed");
}
