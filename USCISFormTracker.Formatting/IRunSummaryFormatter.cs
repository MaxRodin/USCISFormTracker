using USCISFormTracker.Dto;

namespace USCISFormTracker.Formatting;

public interface IRunSummaryFormatter
{
    string FormatAsHtml(RunSummaryMessage summary);
    string FormatAsText(RunSummaryMessage summary);
}
