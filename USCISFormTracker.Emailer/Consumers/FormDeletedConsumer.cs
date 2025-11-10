using System.Text;
using MassTransit;
using USCISFormTracker.Dto;
using USCISFormTracker.Emailer.Models;

namespace USCISFormTracker.Emailer.Consumers;

public class FormDeletedConsumer : IConsumer<FormDeletedMessage>
{
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FormDeletedConsumer> _logger;

    public FormDeletedConsumer(
        IEmailSender emailSender,
        IConfiguration configuration,
        ILogger<FormDeletedConsumer> logger)
    {
        _emailSender = emailSender;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<FormDeletedMessage> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing form deleted notification for {FormName}", message.FormName);

        var toEmails = _configuration.GetSection("EmailNotifications:ToEmails").Get<List<string>>()
            ?? throw new InvalidOperationException("EmailNotifications:ToEmails not configured");

        var subject = $"USCIS Form Removed: {message.FormName}";
        var htmlBody = BuildDeletedFormEmailHtml(message);
        var textBody = BuildDeletedFormEmailText(message);

        foreach (var toEmail in toEmails)
        {
            var emailMessage = new EmailMessage
            {
                To = toEmail,
                Subject = subject,
                HtmlBody = htmlBody,
                TextBody = textBody
            };

            await _emailSender.SendEmailAsync(emailMessage);
            _logger.LogInformation("Form deleted notification email sent to {Email} for {FormName}", toEmail, message.FormName);
        }
    }

    private string BuildDeletedFormEmailHtml(FormDeletedMessage message)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<html><body>");
        sb.AppendLine($"<h2 style=\"color: red;\">USCIS Form Removed: {message.FormName}</h2>");
        sb.AppendLine($"<p><strong>Form Link:</strong> {message.Link}</p>");
        sb.AppendLine($"<p><strong>Last Seen:</strong> {message.LastSeen:yyyy-MM-dd HH:mm:ss} UTC</p>");
        sb.AppendLine("<p>This form is no longer available on the USCIS website and has been removed from monitoring.</p>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private string BuildDeletedFormEmailText(FormDeletedMessage message)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"USCIS Form Removed: {message.FormName}");
        sb.AppendLine($"Form Link: {message.Link}");
        sb.AppendLine($"Last Seen: {message.LastSeen:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        sb.AppendLine("This form is no longer available on the USCIS website and has been removed from monitoring.");
        return sb.ToString();
    }
}
