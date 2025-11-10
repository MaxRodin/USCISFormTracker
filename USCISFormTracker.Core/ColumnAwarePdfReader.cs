using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace USCISFormTracker.Core;

/// <summary>
/// Advanced PDF text extraction that handles multi-column layouts.
/// Extends ImprovedPdfPigReader with intelligent column detection and proper reading order.
///
/// Features:
/// 1. Filters out footers based on Y position from page dimensions
/// 2. Detects column boundaries by analyzing X-position gaps in text
/// 3. Processes each column separately to maintain proper reading order
/// 4. Groups words by Y position within each column to preserve line breaks
/// </summary>
public class ColumnAwarePdfReader : IPdfReader
{
    private readonly double _footerMarginPoints = 80.0;  // Bottom 80 points (about 1.1 inches)
    private readonly double _lineTolerancePoints = 3.0;  // Words within 3 points are on same line
    private readonly double _columnGapMinimumPoints = 50.0; // Minimum gap to consider separate columns (increased from 30)
    private readonly double _gapConsistencyThreshold = 0.10; // Gap must appear in 10% of lines to be a column boundary (reduced from 30%)

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
            .ToList();

        if (!contentWords.Any())
        {
            return string.Empty;
        }

        // Detect columns based on X-position gaps
        var columns = DetectColumns(contentWords);

        // Process each column separately and concatenate
        var sb = new StringBuilder();
        foreach (var columnWords in columns)
        {
            var columnText = ProcessColumn(columnWords);
            sb.Append(columnText);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Detects columns using Y-Level Gap Analysis.
    /// Groups words by line, analyzes gaps within each line, finds consistent column boundaries.
    /// Returns words grouped by column, sorted left to right.
    /// </summary>
    private List<List<Word>> DetectColumns(List<Word> words)
    {
        if (!words.Any())
        {
            return new List<List<Word>>();
        }

        // Step 1: Group words by Y position (same line)
        var lines = GroupWordsByLine(words);

        // Step 2: For each line, find all significant gaps
        var allGaps = new List<(double centerX, double width)>();

        foreach (var line in lines)
        {
            // Sort words in this line left to right
            var sortedLine = line.OrderBy(w => w.BoundingBox.Left).ToList();

            // Find gaps between consecutive words
            for (int i = 0; i < sortedLine.Count - 1; i++)
            {
                var currentWordRight = sortedLine[i].BoundingBox.Right;
                var nextWordLeft = sortedLine[i + 1].BoundingBox.Left;
                var gap = nextWordLeft - currentWordRight;

                // Only consider significant gaps
                if (gap > _columnGapMinimumPoints)
                {
                    var gapCenter = (currentWordRight + nextWordLeft) / 2;
                    allGaps.Add((gapCenter, gap));
                }
            }
        }

        // Step 3: Find consistent column boundaries (gaps that appear frequently)
        var columnBoundaries = FindConsistentGaps(allGaps, lines.Count);

        // Step 4: Group words into columns based on boundaries
        var columns = new List<List<Word>>();

        if (!columnBoundaries.Any())
        {
            // Single column - return all words
            columns.Add(words);
        }
        else
        {
            // Multiple columns - split words by boundaries
            for (int i = 0; i <= columnBoundaries.Count; i++)
            {
                var columnWords = new List<Word>();

                foreach (var word in words)
                {
                    var wordCenter = (word.BoundingBox.Left + word.BoundingBox.Right) / 2;

                    if (i == 0)
                    {
                        // First column: left of first boundary
                        if (wordCenter < columnBoundaries[0])
                        {
                            columnWords.Add(word);
                        }
                    }
                    else if (i == columnBoundaries.Count)
                    {
                        // Last column: right of last boundary
                        if (wordCenter >= columnBoundaries[i - 1])
                        {
                            columnWords.Add(word);
                        }
                    }
                    else
                    {
                        // Middle columns: between boundaries
                        if (wordCenter >= columnBoundaries[i - 1] && wordCenter < columnBoundaries[i])
                        {
                            columnWords.Add(word);
                        }
                    }
                }

                if (columnWords.Any())
                {
                    columns.Add(columnWords);
                }
            }
        }

        return columns;
    }

    /// <summary>
    /// Groups words into lines based on Y position (vertical tolerance).
    /// </summary>
    private List<List<Word>> GroupWordsByLine(List<Word> words)
    {
        var lines = new List<List<Word>>();
        var sortedWords = words.OrderByDescending(w => w.BoundingBox.Bottom).ToList();

        List<Word>? currentLine = null;
        double? currentY = null;

        foreach (var word in sortedWords)
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

        return lines;
    }

    /// <summary>
    /// Finds consistent gaps that appear across multiple lines (column boundaries).
    /// </summary>
    private List<double> FindConsistentGaps(List<(double centerX, double width)> allGaps, int totalLines)
    {
        if (!allGaps.Any())
        {
            return new List<double>();
        }

        // Cluster gaps by center X position (within 40 points = same boundary, increased tolerance)
        var gapClusters = new List<List<double>>();

        foreach (var gap in allGaps)
        {
            var matchingCluster = gapClusters.FirstOrDefault(cluster =>
                cluster.Any(x => Math.Abs(x - gap.centerX) < 40));

            if (matchingCluster != null)
            {
                matchingCluster.Add(gap.centerX);
            }
            else
            {
                gapClusters.Add(new List<double> { gap.centerX });
            }
        }

        // Find clusters that appear in at least X% of lines (consistency threshold)
        var minOccurrences = (int)(totalLines * _gapConsistencyThreshold);
        var consistentBoundaries = gapClusters
            .Where(cluster => cluster.Count >= minOccurrences)
            .Select(cluster => cluster.Average()) // Use average position as boundary
            .OrderBy(x => x)
            .ToList();

        return consistentBoundaries;
    }

    /// <summary>
    /// Process a single column: group words by line (Y position) and build text.
    /// </summary>
    private string ProcessColumn(List<Word> columnWords)
    {
        if (!columnWords.Any())
        {
            return string.Empty;
        }

        // Sort top to bottom, left to right within column
        var sortedWords = columnWords
            .OrderByDescending(w => w.BoundingBox.Bottom) // Top to bottom
            .ThenBy(w => w.BoundingBox.Left)              // Left to right
            .ToList();

        // Group words by line (words with similar Y positions are on the same line)
        var lines = new List<List<Word>>();
        List<Word>? currentLine = null;
        double? currentY = null;

        foreach (var word in sortedWords)
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
