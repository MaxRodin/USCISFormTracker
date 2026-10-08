using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;
using UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector;

namespace USCISFormTracker.Core.PdfReaders;

/// <summary>
/// PDF text extraction using PdfPig's rendering order.
///
/// Uses page.Letters to follow the PDF's internal rendering sequence,
/// then groups letters into words using NearestNeighbourWordExtractor.
/// This naturally produces correct reading order for USCIS forms.
///
/// Approach:
/// 1. Extract letters in their PDF rendering order (page.Letters)
/// 2. Filter out footers based on Y position
/// 3. Group letters into words using NearestNeighbourWordExtractor
/// 4. Detect line breaks by Y position (3pt tolerance)
/// 5. Output text following the natural PDF rendering order
///
/// Why this works:
/// - Respects the order letters were written to the PDF
/// - No complex segmentation or clustering needed
/// - Naturally handles multi-column forms correctly
/// - Simple, maintainable implementation
/// - Produces clean, readable text for change detection
/// </summary>
public class PdfPigLayoutPdfReader : IPdfReader
{
    private readonly double _footerMarginPoints = 80.0;  // Bottom 80 points (about 1.1 inches)

    public string GetPdfText(Stream stream)
    {
        using var document = PdfDocument.Open(stream);
        var sb = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            var pageText = ExtractPageText(page);
            sb.Append(pageText);
        }

        return sb.ToString();
    }

    private string ExtractPageText(Page page)
    {
        // Step 1: Extract words using nearest neighbor algorithm
        // This handles various text orientations and spacing better than default
        var allWords = page.GetWords(NearestNeighbourWordExtractor.Instance).ToList();

        if (!allWords.Any())
        {
            return string.Empty;
        }

        // Step 2: Filter out footers based on Y position
        // Keep existing footer filtering logic for consistency
        var contentWords = allWords
            .Where(w => w.BoundingBox.Bottom > _footerMarginPoints)
            .ToList();

        if (!contentWords.Any())
        {
            return string.Empty;
        }

        // Step 3: Use page.Letters to respect PDF's internal rendering order
        // This follows the exact sequence letters were written to the PDF
        var sb = new StringBuilder();
        var letters = page.Letters.Where(l => l.GlyphRectangle.Bottom > _footerMarginPoints).ToList();

        if (!letters.Any())
        {
            return string.Empty;
        }

        // Group letters into words based on proximity
        var words = NearestNeighbourWordExtractor.Instance.GetWords(letters);

        // Extract text from words in their natural PDF order
        var currentLine = new StringBuilder();
        double? lastY = null;
        const double lineBreakTolerance = 3.0;

        foreach (var word in words)
        {
            var wordY = word.BoundingBox.Bottom;

            // Check if we're on a new line
            if (lastY.HasValue && Math.Abs(wordY - lastY.Value) > lineBreakTolerance)
            {
                sb.AppendLine(currentLine.ToString().Trim());
                currentLine.Clear();
            }

            currentLine.Append(word.Text + " ");
            lastY = wordY;
        }

        // Append final line
        if (currentLine.Length > 0)
        {
            sb.AppendLine(currentLine.ToString().Trim());
        }

        return sb.ToString();
    }
}
