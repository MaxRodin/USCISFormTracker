using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using USCISFormTracker.Core.Models;
using USCISFormTracker.Email;
using USCISFormTracker.Formatting;

namespace USCISFormTracker.Tests;

public class RunSummaryNotifierTests
{
    private static readonly MailgunOptions Options = new()
    {
        ApiKey = "key",
        Domain = "mg.example.com",
        FromEmail = "noreply@example.com",
        FromName = "USCIS Form Tracker",
        MailingListAddress = "list@mg.example.com"
    };

    private static RunSummaryNotifier CreateNotifier(Mock<IEmailSender> sender) =>
        new(sender.Object, new RunSummaryFormatter(), Options, NullLogger<RunSummaryNotifier>.Instance);

    [Fact]
    public async Task SendAsync_NoChanges_SendsNothing()
    {
        var sender = new Mock<IEmailSender>();
        var summary = new FormRunSummary { RunTime = DateTime.UtcNow, TotalFormsOnWebsite = 5 };

        await CreateNotifier(sender).SendAsync(summary);

        sender.Verify(s => s.SendEmailAsync(It.IsAny<EmailMessage>()), Times.Never);
    }

    [Fact]
    public async Task SendAsync_WithChanges_SendsOneEmailToMailingList()
    {
        var sender = new Mock<IEmailSender>();
        EmailMessage? sent = null;
        sender.Setup(s => s.SendEmailAsync(It.IsAny<EmailMessage>()))
            .Callback<EmailMessage>(m => sent = m)
            .Returns(Task.CompletedTask);

        var summary = new FormRunSummary
        {
            RunTime = DateTime.UtcNow,
            TotalFormsOnWebsite = 5,
            AddedForms =
            {
                new AddedForm { FileName = "i-90.pdf", FormName = "I-90", FullLink = "https://example.com/i-90.pdf", Hash = "h", ExtractedText = "t" },
                new AddedForm { FileName = "i-130.pdf", FormName = "I-130", FullLink = "https://example.com/i-130.pdf", Hash = "h", ExtractedText = "t" }
            },
            DeletedForms =
            {
                new DeletedForm { FileName = "i-485.pdf", FormName = "I-485", LastKnownLink = "https://example.com/i-485.pdf" }
            }
        };

        await CreateNotifier(sender).SendAsync(summary);

        sender.Verify(s => s.SendEmailAsync(It.IsAny<EmailMessage>()), Times.Once);
        Assert.NotNull(sent);
        Assert.Equal("list@mg.example.com", sent!.To);
        Assert.Equal("USCIS Form Tracker - Daily Summary (2 new, 0 changed, 1 deleted)", sent.Subject);
        Assert.Contains("I-90", sent.HtmlBody);
        Assert.Contains("I-485", sent.TextBody);
    }
}
