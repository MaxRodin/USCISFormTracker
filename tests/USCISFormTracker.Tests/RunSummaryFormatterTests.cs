using USCISFormTracker.Core.Models;
using USCISFormTracker.Formatting;

namespace USCISFormTracker.Tests;

public class RunSummaryFormatterTests
{
    private static FormRunSummary BuildSummary() => new()
    {
        RunTime = new DateTime(2026, 10, 8, 2, 0, 0, DateTimeKind.Utc),
        TotalFormsOnWebsite = 3,
        AddedForms =
        {
            new AddedForm
            {
                FileName = "i-90.pdf",
                FormName = "I-90 <Replacement>",
                FullLink = "https://www.uscis.gov/sites/default/files/document/forms/i-90.pdf",
                Hash = "abc",
                ExtractedText = "text"
            }
        },
        ChangedForms =
        {
            new ChangedForm
            {
                FileName = "i-130.pdf",
                FormName = "I-130",
                FullLink = "https://www.uscis.gov/sites/default/files/document/forms/i-130.pdf",
                OldHash = "old",
                NewHash = "new",
                OldText = "old text",
                NewText = "new text",
                Diff = new DiffLines
                {
                    AddedLines = { "Edition 10/01/2026" },
                    DeletedLines = { "Edition 01/01/2025" }
                }
            }
        },
        DeletedForms =
        {
            new DeletedForm
            {
                FileName = "i-485.pdf",
                FormName = "I-485",
                LastKnownLink = "https://www.uscis.gov/sites/default/files/document/forms/i-485.pdf"
            }
        }
    };

    [Fact]
    public void FormatAsHtml_IncludesCountsSectionsAndDiff()
    {
        var html = new RunSummaryFormatter().FormatAsHtml(BuildSummary());

        Assert.Contains("<strong>3</strong> total forms", html);
        Assert.Contains("1</strong> new forms discovered", html);
        Assert.Contains("1</strong> forms changed", html);
        Assert.Contains("1</strong> forms removed", html);
        Assert.Contains("+ Edition 10/01/2026", html);
        Assert.Contains("- Edition 01/01/2025", html);
        Assert.Contains("I-485", html);
    }

    [Fact]
    public void FormatAsHtml_EncodesFormNames()
    {
        var html = new RunSummaryFormatter().FormatAsHtml(BuildSummary());

        Assert.Contains("I-90 &lt;Replacement&gt;", html);
        Assert.DoesNotContain("I-90 <Replacement>", html);
    }

    [Fact]
    public void FormatAsText_IncludesCountsSectionsAndDiff()
    {
        var text = new RunSummaryFormatter().FormatAsText(BuildSummary());

        Assert.Contains("Total forms on website: 3", text);
        Assert.Contains("New forms: 1", text);
        Assert.Contains("Changed forms: 1", text);
        Assert.Contains("Removed forms: 1", text);
        Assert.Contains("+ Edition 10/01/2026", text);
        Assert.Contains("- Edition 01/01/2025", text);
        Assert.Contains("I-485 (i-485.pdf)", text);
    }
}
