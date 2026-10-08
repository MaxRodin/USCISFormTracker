using USCISFormTracker.Core.Models;

namespace USCISFormTracker.Formatting;

public interface IRunSummaryFormatter
{
    string FormatAsHtml(FormRunSummary summary);
    string FormatAsText(FormRunSummary summary);
}
