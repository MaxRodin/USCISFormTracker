using System.Text;
using MassTransit;
using USCISFormTracker.Dto;
using USCISFormTracker.Emailer.Models;

namespace USCISFormTracker.Emailer.Consumers;

public class FormAddedConsumer : IConsumer<FormAddedMessage>
{
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FormAddedConsumer> _logger;

    public FormAddedConsumer(
        IEmailSender emailSender,
        IConfiguration configuration,
        ILogger<FormAddedConsumer> logger)
    {
        _emailSender = emailSender;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<FormAddedMessage> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing form added notification for {FormName}", message.FormName);

        var mailingListAddress = _configuration["Mailgun:MailingListAddress"]
            ?? throw new InvalidOperationException("Mailgun:MailingListAddress not configured");

        var subject = $"New USCIS Form Added: {message.FormName}";
        var htmlBody = BuildAddedFormEmailHtml(message);
        var textBody = BuildAddedFormEmailText(message);

        var emailMessage = new EmailMessage
        {
            To = mailingListAddress,
            Subject = subject,
            HtmlBody = htmlBody,
            TextBody = textBody
        };

        await _emailSender.SendEmailAsync(emailMessage);
        _logger.LogInformation("Form added notification email sent to mailing list for {FormName}", message.FormName);
    }

    private string BuildAddedFormEmailHtml(FormAddedMessage newForm)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<html><body>");
        sb.AppendLine($"<h2>New USCIS Form Added: {newForm.FormName}</h2>");
        sb.AppendLine($"<p><strong>Form Link:</strong> <a href=\"{newForm.FullLink}\">{newForm.FullLink}</a></p>");
        sb.AppendLine($"<p><strong>Discovered:</strong> {newForm.DiscoveredTime:yyyy-MM-dd HH:mm:ss} UTC</p>");
        sb.AppendLine($"<p><strong>Hash:</strong> {newForm.Hash}</p>");
        sb.AppendLine("<p>This form has been added to the monitoring system.</p>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private string BuildAddedFormEmailText(FormAddedMessage newForm)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"New USCIS Form Added: {newForm.FormName}");
        sb.AppendLine($"Form Link: {newForm.FullLink}");
        sb.AppendLine($"Discovered: {newForm.DiscoveredTime:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"Hash: {newForm.Hash}");
        sb.AppendLine();
        sb.AppendLine("This form has been added to the monitoring system.");
        return sb.ToString();
    }
}
