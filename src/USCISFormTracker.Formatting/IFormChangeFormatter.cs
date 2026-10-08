using USCISFormTracker.Core.Models;

namespace USCISFormTracker.Formatting;

public interface IFormChangeFormatter
{
    string FormatAsHtml(PdfFormChange change);
    string FormatAsText(PdfFormChange change);
}
