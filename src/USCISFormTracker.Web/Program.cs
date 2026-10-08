using MassTransit;
using Microsoft.EntityFrameworkCore;
using USCISFormTracker.Core;
using USCISFormTracker.Data;
using USCISFormTracker.Dto;
using USCISFormTracker.Formatting;
using DotNetEnv;
using System.ComponentModel.DataAnnotations;

// Load environment variables from .env file (searching parent directories,
// since `dotnet run` sets the working directory to the project directory)
Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel. HTTPS is enabled only when a certificate is present
// (e.g., a Cloudflare Origin Certificate mounted at /app/certs in Docker),
// so the app runs HTTP-only out of the box.
var httpPort = int.TryParse(builder.Configuration["HTTP_PORT"], out var configuredPort) ? configuredPort : 80;
var httpsCertPath = builder.Configuration["HTTPS_CERT_PATH"] ?? "/app/certs/origin.pfx";
var useHttps = File.Exists(httpsCertPath);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(httpPort);

    if (useHttps)
    {
        options.ListenAnyIP(443, listenOptions =>
        {
            listenOptions.UseHttps(httpsCertPath);
        });
    }
});

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Data layer (DbContext + Repository)
builder.Services.AddDataServices(builder.Configuration);

// Formatting services
builder.Services.AddSingleton<IFormChangeFormatter, FormChangeFormatter>();

// Configure MassTransit with RabbitMQ
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

// Apply database migrations
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<FormTrackerDbContext>();
    await dbContext.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Redirect HTTP to HTTPS (Full mode with origin certificate)
if (useHttps)
{
    app.UseHttpsRedirection();
}
else
{
    app.Logger.LogInformation("No HTTPS certificate found at {CertPath}; serving HTTP only on port {Port}", httpsCertPath, httpPort);
}

// Serve static files (index.html, images, etc.)
app.UseDefaultFiles(); // Serves index.html by default
app.UseStaticFiles();

// API endpoints below
// AddToMailingList endpoint
app.MapPost("/mailing-list", async (EmailSubscriptionRequest request, IPublishEndpoint publishEndpoint) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
    {
        return Results.BadRequest(new { error = "Invalid email address" });
    }

    var message = new AddToMailingListMessage
    {
        Email = request.Email,
        SubscribedAt = DateTime.UtcNow
    };

    await publishEndpoint.Publish(message);

    return Results.Ok(new { message = "Successfully added to mailing list", email = request.Email });
})
.WithName("AddToMailingList")
.WithOpenApi();

// GetMostRecentChange endpoint
app.MapGet("/changes/recent", async (IFormRepository repository, IFormChangeFormatter formatter) =>
{
    var changes = await repository.GetRecentChangesAsync(1);

    if (changes.Count == 0)
    {
        return Results.Content("<html><body><h2>No recent changes found</h2></body></html>", "text/html");
    }

    var mostRecent = changes[0];
    var html = formatter.FormatAsHtml(mostRecent);

    return Results.Content(html, "text/html");
})
.WithName("GetMostRecentChange")
.WithOpenApi();

app.Run();

// Request model for email subscription
record EmailSubscriptionRequest(
    [EmailAddress][Required] string Email
);
