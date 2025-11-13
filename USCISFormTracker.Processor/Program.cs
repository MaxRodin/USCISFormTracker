using DotNetEnv;
using MassTransit;
using Quartz;
using USCISFormTracker.Core;
using USCISFormTracker.Processor;
using USCISFormTracker.Processor.Jobs;
using USCISFormTracker.Dto;

// Load environment variables from .env file
Env.Load();

var builder = Host.CreateApplicationBuilder(args);

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

var host = builder.Build();
host.Run();
