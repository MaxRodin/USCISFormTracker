using MassTransit;
using Microsoft.EntityFrameworkCore;
using USCISFormTracker.Core.Data;
using USCISFormTracker.Dto;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<FormTrackerDbContext>(options =>
    options.UseSqlite(connectionString));

// Repository
builder.Services.AddScoped<IFormRepository, FormRepository>();

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

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<FormTrackerDbContext>();
    dbContext.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// AddToMailingList endpoint
app.MapPost("/mailing-list", async (string email, IPublishEndpoint publishEndpoint) =>
{
    if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
    {
        return Results.BadRequest(new { error = "Invalid email address" });
    }

    var message = new AddToMailingListMessage
    {
        Email = email,
        SubscribedAt = DateTime.UtcNow
    };

    await publishEndpoint.Publish(message);

    return Results.Ok(new { message = "Successfully added to mailing list", email });
})
.WithName("AddToMailingList")
.WithOpenApi();

// GetMostRecentChange endpoint
app.MapGet("/changes/recent", async (IFormRepository repository) =>
{
    var changes = await repository.GetRecentChangesAsync(1);

    if (changes.Count == 0)
    {
        return Results.NotFound(new { message = "No changes found" });
    }

    var mostRecent = changes[0];

    return Results.Ok(new
    {
        fileName = mostRecent.FileName,
        fullLink = mostRecent.FullLink,
        formName = mostRecent.FormName,
        oldHash = mostRecent.OldHash,
        newHash = mostRecent.NewHash,
        diffLinesSerialized = mostRecent.DiffLinesSerialized,
        detectedChangeTime = mostRecent.DetectedChangeTime
    });
})
.WithName("GetMostRecentChange")
.WithOpenApi();

app.Run();
