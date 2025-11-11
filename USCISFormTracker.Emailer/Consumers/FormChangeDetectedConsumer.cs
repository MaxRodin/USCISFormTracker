using System.Text;
using MassTransit;
using USCISFormTracker.Dto;
using USCISFormTracker.Emailer.Models;

namespace USCISFormTracker.Emailer.Consumers;

public class FormChangeDetectedConsumer : IConsumer<FormChangeDetectedMessage>
{
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FormChangeDetectedConsumer> _logger;

    public FormChangeDetectedConsumer(
        IEmailSender emailSender,
        IConfiguration configuration,
        ILogger<FormChangeDetectedConsumer> logger)
    {
        _emailSender = emailSender;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<FormChangeDetectedMessage> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing form change detected for {FormName}", message.FormName);

        var mailingListAddress = _configuration["Mailgun:MailingListAddress"]
            ?? throw new InvalidOperationException("Mailgun:MailingListAddress not configured");

        var subject = $"USCIS Form Change: {message.FormName}";

        // Apply line limit (75-100 total lines recommended)
        var maxLinesPerSection = 25;
        var limitedAdded = message.AddedLines.Take(maxLinesPerSection).ToList();
        var limitedDeleted = message.DeletedLines.Take(maxLinesPerSection).ToList();
        var limitedModified = message.ModifiedLines.Take(maxLinesPerSection).ToList();

        var totalShown = limitedAdded.Count + limitedDeleted.Count + limitedModified.Count;
        var totalChanges = message.AddedLines.Count + message.DeletedLines.Count + message.ModifiedLines.Count;

        var htmlBody = BuildChangeEmailHtml(message, limitedAdded, limitedDeleted, limitedModified, totalShown, totalChanges);
        var textBody = BuildChangeEmailText(message, limitedAdded, limitedDeleted, limitedModified, totalShown, totalChanges);

        var emailMessage = new EmailMessage
        {
            To = mailingListAddress,
            Subject = subject,
            HtmlBody = htmlBody,
            TextBody = textBody
        };

        await _emailSender.SendEmailAsync(emailMessage);
        _logger.LogInformation("Change notification email sent to mailing list for {FormName}", message.FormName);
    }

    private string BuildChangeEmailHtml(
        FormChangeDetectedMessage change,
        List<string> addedLines,
        List<string> deletedLines,
        List<string> modifiedLines,
        int totalShown,
        int totalChanges)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<html><body>");
        sb.AppendLine($"<h2>USCIS Form Change Detected: {change.FormName}</h2>");
        sb.AppendLine($"<p><strong>Form Link:</strong> <a href=\"{change.FullLink}\">{change.FullLink}</a></p>");
        sb.AppendLine($"<p><strong>Detected:</strong> {change.DetectedChangeTime:yyyy-MM-dd HH:mm:ss} UTC</p>");
        sb.AppendLine($"<p><strong>Old Hash:</strong> {change.OldHash}</p>");
        sb.AppendLine($"<p><strong>New Hash:</strong> {change.NewHash}</p>");

        if (totalShown < totalChanges)
        {
            sb.AppendLine($"<p><em>Showing {totalShown} of {totalChanges} total changes</em></p>");
        }

        if (addedLines.Count > 0)
        {
            sb.AppendLine("<h3 style=\"color: green;\">Added Lines:</h3>");
            sb.AppendLine("<ul>");
            foreach (var line in addedLines)
            {
                sb.AppendLine($"<li>{System.Net.WebUtility.HtmlEncode(line)}</li>");
            }
            if (change.AddedLines.Count > addedLines.Count)
            {
                sb.AppendLine($"<li><em>... and {change.AddedLines.Count - addedLines.Count} more</em></li>");
            }
            sb.AppendLine("</ul>");
        }

        if (deletedLines.Count > 0)
        {
            sb.AppendLine("<h3 style=\"color: red;\">Deleted Lines:</h3>");
            sb.AppendLine("<ul>");
            foreach (var line in deletedLines)
            {
                sb.AppendLine($"<li>{System.Net.WebUtility.HtmlEncode(line)}</li>");
            }
            if (change.DeletedLines.Count > deletedLines.Count)
            {
                sb.AppendLine($"<li><em>... and {change.DeletedLines.Count - deletedLines.Count} more</em></li>");
            }
            sb.AppendLine("</ul>");
        }

        if (modifiedLines.Count > 0)
        {
            sb.AppendLine("<h3 style=\"color: orange;\">Modified Lines:</h3>");
            sb.AppendLine("<ul>");
            foreach (var line in modifiedLines)
            {
                sb.AppendLine($"<li>{System.Net.WebUtility.HtmlEncode(line)}</li>");
            }
            if (change.ModifiedLines.Count > modifiedLines.Count)
            {
                sb.AppendLine($"<li><em>... and {change.ModifiedLines.Count - modifiedLines.Count} more</em></li>");
            }
            sb.AppendLine("</ul>");
        }

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private string BuildChangeEmailText(
        FormChangeDetectedMessage change,
        List<string> addedLines,
        List<string> deletedLines,
        List<string> modifiedLines,
        int totalShown,
        int totalChanges)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"USCIS Form Change Detected: {change.FormName}");
        sb.AppendLine($"Form Link: {change.FullLink}");
        sb.AppendLine($"Detected: {change.DetectedChangeTime:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"Old Hash: {change.OldHash}");
        sb.AppendLine($"New Hash: {change.NewHash}");
        sb.AppendLine();

        if (totalShown < totalChanges)
        {
            sb.AppendLine($"(Showing {totalShown} of {totalChanges} total changes)");
            sb.AppendLine();
        }

        if (addedLines.Count > 0)
        {
            sb.AppendLine("=== Added Lines ===");
            foreach (var line in addedLines)
            {
                sb.AppendLine($"+ {line}");
            }
            if (change.AddedLines.Count > addedLines.Count)
            {
                sb.AppendLine($"... and {change.AddedLines.Count - addedLines.Count} more");
            }
            sb.AppendLine();
        }

        if (deletedLines.Count > 0)
        {
            sb.AppendLine("=== Deleted Lines ===");
            foreach (var line in deletedLines)
            {
                sb.AppendLine($"- {line}");
            }
            if (change.DeletedLines.Count > deletedLines.Count)
            {
                sb.AppendLine($"... and {change.DeletedLines.Count - deletedLines.Count} more");
            }
            sb.AppendLine();
        }

        if (modifiedLines.Count > 0)
        {
            sb.AppendLine("=== Modified Lines ===");
            foreach (var line in modifiedLines)
            {
                sb.AppendLine($"~ {line}");
            }
            if (change.ModifiedLines.Count > modifiedLines.Count)
            {
                sb.AppendLine($"... and {change.ModifiedLines.Count - modifiedLines.Count} more");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
