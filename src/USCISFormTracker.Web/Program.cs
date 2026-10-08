using Microsoft.EntityFrameworkCore;
using USCISFormTracker.Core;
using USCISFormTracker.Data;
using USCISFormTracker.Email;
using USCISFormTracker.Formatting;
using DotNetEnv;
using System.ComponentModel.DataAnnotations;

// Load environment variables from .env file (searching parent directories,
// since `dotnet run` sets the working directory to the project directory)
Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel. HTTPS is enabled only when a certificate file exists
// at the configured path; otherwise the app serves HTTP only.
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

// Data layer
builder.Services.AddDataServices(builder.Configuration);

// Formatting services
builder.Services.AddSingleton<IFormChangeFormatter, FormChangeFormatter>();

// Email services (Mailgun)
builder.Services.AddEmailServices(builder.Configuration);

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

// Redirect HTTP to HTTPS when a certificate is in use
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
app.MapPost("/mailing-list", async (EmailSubscriptionRequest request, IEmailSender emailSender, ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
    {
        return Results.BadRequest(new { error = "Invalid email address" });
    }

    try
    {
        await emailSender.AddToMailingListAsync(request.Email);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to add {Email} to the mailing list", request.Email);
        return Results.Problem("Could not add email to mailing list. Please try again later.", statusCode: 502);
    }

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
