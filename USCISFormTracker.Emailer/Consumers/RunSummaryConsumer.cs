using MassTransit;
using USCISFormTracker.Dto;
using USCISFormTracker.Emailer.Models;
using USCISFormTracker.Emailer.Services;

namespace USCISFormTracker.Emailer.Consumers;

public class RunSummaryConsumer : IConsumer<RunSummaryMessage>
{
    private readonly IEmailSender _emailSender;
    private readonly IEmailContentBuilder _contentBuilder;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RunSummaryConsumer> _logger;

    public RunSummaryConsumer(
        IEmailSender emailSender,
        IEmailContentBuilder contentBuilder,
        IConfiguration configuration,
        ILogger<RunSummaryConsumer> logger)
    {
        _emailSender = emailSender;
        _contentBuilder = contentBuilder;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<RunSummaryMessage> context)
    {
        var message = context.Message;
        _logger.LogInformation(
            "Processing run summary: {NewCount} new, {ChangedCount} changed, {DeletedCount} deleted",
            message.NewFormsCount,
            message.ChangedFormsCount,
            message.DeletedFormsCount);

        var mailingListAddress = _configuration["Mailgun:MailingListAddress"]
            ?? throw new InvalidOperationException("Mailgun:MailingListAddress not configured");

        // Build email content using the builder service
        var (subject, htmlBody, textBody) = _contentBuilder.BuildRunSummaryEmail(message);

        var emailMessage = new EmailMessage
        {
            To = mailingListAddress,
            Subject = subject,
            HtmlBody = htmlBody,
            TextBody = textBody
        };

        await _emailSender.SendEmailAsync(emailMessage);
        _logger.LogInformation("Run summary email sent to mailing list");
    }
}
