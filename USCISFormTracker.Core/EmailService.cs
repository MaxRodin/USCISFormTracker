using System.Text;
using USCISFormTracker.Core.Models;

namespace USCISFormTracker.Core;

public class EmailService : IEmailService
{
    private readonly IEmailSender _emailSender;
    private readonly List<string> _toEmails;
    private readonly string _subjectTemplate;

    public EmailService(IEmailSender emailSender, List<string> toEmails, string subjectTemplate)
    {
        _emailSender = emailSender;
        _toEmails = toEmails;
        _subjectTemplate = subjectTemplate;
    }

    public async Task SendChangeNotificationAsync(PdfFormChange change, DiffLines diffLines)
    {
        var subject = _subjectTemplate.Replace("{FormName}", change.FormName);
        var htmlBody = BuildChangeEmailHtml(change, diffLines);
        var textBody = BuildChangeEmailText(change, diffLines);

        foreach (var toEmail in _toEmails)
        {
            var message = new EmailMessage
            {
                To = toEmail,
                Subject = subject,
                HtmlBody = htmlBody,
                TextBody = textBody
            };

            await _emailSender.SendEmailAsync(message);
        }
    }

    public async Task SendAddedFormNotificationAsync(PdfFormRecord newForm)
    {
        var subject = $"New USCIS Form Added: {newForm.FormName}";
        var htmlBody = BuildAddedFormEmailHtml(newForm);
        var textBody = BuildAddedFormEmailText(newForm);

        foreach (var toEmail in _toEmails)
        {
            var message = new EmailMessage
            {
                To = toEmail,
                Subject = subject,
                HtmlBody = htmlBody,
                TextBody = textBody
            };

            await _emailSender.SendEmailAsync(message);
        }
    }

    public async Task SendDeletedFormNotificationAsync(string link, string formName, DateTime lastSeen)
    {
        var subject = $"USCIS Form Removed: {formName}";
        var htmlBody = BuildDeletedFormEmailHtml(link, formName, lastSeen);
        var textBody = BuildDeletedFormEmailText(link, formName, lastSeen);

        foreach (var toEmail in _toEmails)
        {
            var message = new EmailMessage
            {
                To = toEmail,
                Subject = subject,
                HtmlBody = htmlBody,
                TextBody = textBody
            };

            await _emailSender.SendEmailAsync(message);
        }
    }

    private string BuildChangeEmailHtml(PdfFormChange change, DiffLines diffLines)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<html><body>");
        sb.AppendLine($"<h2>USCIS Form Change Detected: {change.FormName}</h2>");
        sb.AppendLine($"<p><strong>Form Link:</strong> <a href=\"{change.Link}\">{change.Link}</a></p>");
        sb.AppendLine($"<p><strong>Detected:</strong> {change.DetectedChangeTime:yyyy-MM-dd HH:mm:ss} UTC</p>");
        sb.AppendLine($"<p><strong>Old Hash:</strong> {change.OldHash}</p>");
        sb.AppendLine($"<p><strong>New Hash:</strong> {change.NewHash}</p>");

        if (diffLines.AddedLines.Count > 0)
        {
            sb.AppendLine("<h3 style=\"color: green;\">Added Lines:</h3>");
            sb.AppendLine("<ul>");
            foreach (var line in diffLines.AddedLines)
            {
                sb.AppendLine($"<li>{System.Net.WebUtility.HtmlEncode(line)}</li>");
            }
            sb.AppendLine("</ul>");
        }

        if (diffLines.DeletedLines.Count > 0)
        {
            sb.AppendLine("<h3 style=\"color: red;\">Deleted Lines:</h3>");
            sb.AppendLine("<ul>");
            foreach (var line in diffLines.DeletedLines)
            {
                sb.AppendLine($"<li>{System.Net.WebUtility.HtmlEncode(line)}</li>");
            }
            sb.AppendLine("</ul>");
        }

        if (diffLines.ModifiedLines.Count > 0)
        {
            sb.AppendLine("<h3 style=\"color: orange;\">Modified Lines:</h3>");
            sb.AppendLine("<ul>");
            foreach (var line in diffLines.ModifiedLines)
            {
                sb.AppendLine($"<li>{System.Net.WebUtility.HtmlEncode(line)}</li>");
            }
            sb.AppendLine("</ul>");
        }

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private string BuildChangeEmailText(PdfFormChange change, DiffLines diffLines)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"USCIS Form Change Detected: {change.FormName}");
        sb.AppendLine($"Form Link: {change.Link}");
        sb.AppendLine($"Detected: {change.DetectedChangeTime:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"Old Hash: {change.OldHash}");
        sb.AppendLine($"New Hash: {change.NewHash}");
        sb.AppendLine();

        if (diffLines.AddedLines.Count > 0)
        {
            sb.AppendLine("=== Added Lines ===");
            foreach (var line in diffLines.AddedLines)
            {
                sb.AppendLine($"+ {line}");
            }
            sb.AppendLine();
        }

        if (diffLines.DeletedLines.Count > 0)
        {
            sb.AppendLine("=== Deleted Lines ===");
            foreach (var line in diffLines.DeletedLines)
            {
                sb.AppendLine($"- {line}");
            }
            sb.AppendLine();
        }

        if (diffLines.ModifiedLines.Count > 0)
        {
            sb.AppendLine("=== Modified Lines ===");
            foreach (var line in diffLines.ModifiedLines)
            {
                sb.AppendLine($"~ {line}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private string BuildAddedFormEmailHtml(PdfFormRecord newForm)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<html><body>");
        sb.AppendLine($"<h2>New USCIS Form Added: {newForm.FormName}</h2>");
        sb.AppendLine($"<p><strong>Form Link:</strong> <a href=\"{newForm.Link}\">{newForm.Link}</a></p>");
        sb.AppendLine($"<p><strong>Discovered:</strong> {newForm.LastChecked:yyyy-MM-dd HH:mm:ss} UTC</p>");
        sb.AppendLine($"<p><strong>Hash:</strong> {newForm.Hash}</p>");
        sb.AppendLine("<p>This form has been added to the monitoring system.</p>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private string BuildAddedFormEmailText(PdfFormRecord newForm)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"New USCIS Form Added: {newForm.FormName}");
        sb.AppendLine($"Form Link: {newForm.Link}");
        sb.AppendLine($"Discovered: {newForm.LastChecked:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"Hash: {newForm.Hash}");
        sb.AppendLine();
        sb.AppendLine("This form has been added to the monitoring system.");
        return sb.ToString();
    }

    private string BuildDeletedFormEmailHtml(string link, string formName, DateTime lastSeen)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<html><body>");
        sb.AppendLine($"<h2 style=\"color: red;\">USCIS Form Removed: {formName}</h2>");
        sb.AppendLine($"<p><strong>Form Link:</strong> {link}</p>");
        sb.AppendLine($"<p><strong>Last Seen:</strong> {lastSeen:yyyy-MM-dd HH:mm:ss} UTC</p>");
        sb.AppendLine("<p>This form is no longer available on the USCIS website and has been removed from monitoring.</p>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private string BuildDeletedFormEmailText(string link, string formName, DateTime lastSeen)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"USCIS Form Removed: {formName}");
        sb.AppendLine($"Form Link: {link}");
        sb.AppendLine($"Last Seen: {lastSeen:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        sb.AppendLine("This form is no longer available on the USCIS website and has been removed from monitoring.");
        return sb.ToString();
    }
}
