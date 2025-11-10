using System.Text;
using UglyToad.PdfPig;

namespace USCISFormTracker.Core.PdfReaders;

public class PdfPigReader : IPdfReader
{
    public string GetPdfText(Stream stream)
    {
        using var document = PdfDocument.Open(stream);
        var sb = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            sb.AppendLine(page.Text);
        }

        return sb.ToString();
    }
}
