namespace USCISFormTracker.Core.Models;

public class EmailMessage
{
    public required string To { get; set; }
    public required string Subject { get; set; }
    public required string HtmlBody { get; set; }
    public string? TextBody { get; set; }
}
