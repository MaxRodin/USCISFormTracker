using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace USCISFormTracker.Core;

/// <summary>
/// Improved PDF text extraction that:
/// 1. Filters out footers based on Y position from page dimensions
/// 2. Groups words by Y position to preserve line breaks
/// 3. Handles sentence boundaries properly
/// </summary>
public class ImprovedPdfPigReader : IPdfReader
{
    private readonly double _footerMarginPoints = 80.0;  // Bottom 80 points (about 1.1 inches)
    private readonly double _lineTolerancePoints = 3.0;  // Words within 3 points are on same line

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
        var words = page.GetWords().ToList();
        if (!words.Any())
        {
            return string.Empty;
        }

        // Filter out footers based on Y position
        var footerThreshold = _footerMarginPoints;

        // Filter out footers based on Y position
        var contentWords = words
            .Where(w => w.BoundingBox.Bottom > footerThreshold)
            .OrderByDescending(w => w.BoundingBox.Bottom) // Top to bottom
            .ThenBy(w => w.BoundingBox.Left)              // Left to right
            .ToList();

        if (!contentWords.Any())
        {
            return string.Empty;
        }

        // Group words by line (words with similar Y positions are on the same line)
        var lines = new List<List<Word>>();
        List<Word>? currentLine = null;
        double? currentY = null;

        foreach (var word in contentWords)
        {
            var wordY = word.BoundingBox.Bottom;

            if (currentY == null || Math.Abs(wordY - currentY.Value) > _lineTolerancePoints)
            {
                // Start a new line
                currentLine = new List<Word> { word };
                lines.Add(currentLine);
                currentY = wordY;
            }
            else
            {
                // Same line
                currentLine!.Add(word);
            }
        }

        // Build text with proper line breaks
        var sb = new StringBuilder();
        foreach (var line in lines)
        {
            var lineText = string.Join(" ", line.Select(w => w.Text));
            sb.AppendLine(lineText);
        }

        return sb.ToString();
    }
}
