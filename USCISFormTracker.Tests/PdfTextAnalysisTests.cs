using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using USCISFormTracker.Core;
using USCISFormTracker.Core.PdfReaders;
using USCISFormTracker.Tests.TestHelpers;
using Xunit.Abstractions;

namespace USCISFormTracker.Tests;

/// <summary>
/// Deep analysis of PDF text extraction to understand PdfPig's behavior
/// </summary>
public class PdfTextAnalysisTests
{
    private readonly ITestOutputHelper _output;

    public PdfTextAnalysisTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void AnalyzePdfStructure_FirstVersion()
    {
        // Arrange
        var pdfBytes = TestDataLoader.LoadPdfFile("PdfTest_First.pdf");

        // Act - Use PdfPig directly to analyze structure
        using var stream = new MemoryStream(pdfBytes);
        using var document = PdfDocument.Open(stream);

        _output.WriteLine("=== PDF STRUCTURE ANALYSIS - FIRST VERSION ===\n");
        _output.WriteLine($"Total Pages: {document.NumberOfPages}");

        foreach (var page in document.GetPages())
        {
            _output.WriteLine($"\n--- PAGE {page.Number} ---");
            _output.WriteLine($"Page dimensions: {page.Width} x {page.Height}");
            _output.WriteLine($"Total words: {page.GetWords().Count()}");

            _output.WriteLine("\n=== SIMPLE TEXT EXTRACTION (page.Text) ===");
            _output.WriteLine($"Length: {page.Text.Length} chars");
            _output.WriteLine($"Content: '{page.Text}'");
            _output.WriteLine($"Hex: {BitConverter.ToString(System.Text.Encoding.UTF8.GetBytes(page.Text)).Replace("-", " ")}");

            _output.WriteLine("\n=== WORD-BY-WORD ANALYSIS ===");
            var words = page.GetWords().ToList();
            for (int i = 0; i < words.Count; i++)
            {
                var word = words[i];
                _output.WriteLine($"Word {i + 1}: '{word.Text}' at Y={word.BoundingBox.Bottom:F2} (letters: {word.Letters.Count})");
            }

            _output.WriteLine("\n=== LETTER-BY-LETTER ANALYSIS ===");
            var letters = page.Letters.ToList();
            _output.WriteLine($"Total letters: {letters.Count}");
            for (int i = 0; i < Math.Min(letters.Count, 50); i++)
            {
                var letter = letters[i];
                _output.WriteLine($"Letter {i + 1}: '{letter.Value}' at ({letter.Location.X:F2}, {letter.Location.Y:F2})");
            }
        }
    }

    [Fact]
    public void CompareCurrentExtraction_WithIdealOutput()
    {
        // Arrange
        var pdfReader = new PdfPigReader();
        var pdfBytes = TestDataLoader.LoadPdfFile("PdfTest_First.pdf");

        // Act - Current extraction
        using var stream = new MemoryStream(pdfBytes);
        var actualText = pdfReader.GetPdfText(stream);

        // What we SHOULD get (ideal output based on user's description)
        var idealText = @"This line is static.
This line will change.
We are going to delete this line.

Another page";

        // Output comparison
        _output.WriteLine("=== CURRENT EXTRACTION ===");
        _output.WriteLine(actualText);
        _output.WriteLine($"\nLength: {actualText.Length} chars");
        _output.WriteLine($"Lines: {actualText.Split('\n').Length}");

        _output.WriteLine("\n=== IDEAL EXTRACTION ===");
        _output.WriteLine(idealText);
        _output.WriteLine($"\nLength: {idealText.Length} chars");
        _output.WriteLine($"Lines: {idealText.Split('\n').Length}");

        _output.WriteLine("\n=== ISSUES IDENTIFIED ===");
        _output.WriteLine("1. Headers/footers ('OriginalHeader', '10/11/2025') mixed into content");
        _output.WriteLine("2. Sentences not split at periods - all on one line");
        _output.WriteLine("3. Multiple logical content pieces concatenated with spaces");
    }

    [Fact]
    public void ExperimentWithContentExtraction_UsingLetterPositions()
    {
        // This test experiments with using letter positions to better extract text
        var pdfBytes = TestDataLoader.LoadPdfFile("PdfTest_First.pdf");

        using var stream = new MemoryStream(pdfBytes);
        using var document = PdfDocument.Open(stream);

        _output.WriteLine("=== EXPERIMENTAL EXTRACTION USING LETTER POSITIONS ===\n");

        foreach (var page in document.GetPages())
        {
            var letters = page.Letters.OrderBy(l => l.Location.Y).ThenBy(l => l.Location.X).ToList();

            _output.WriteLine($"Page {page.Number}: {letters.Count} letters");
            _output.WriteLine("\nLetter positions (first 30):");

            foreach (var letter in letters.Take(30))
            {
                _output.WriteLine($"  '{letter.Value}' at Y={letter.Location.Y:F2}");
            }

            // Try to identify headers/footers by Y position
            var minY = letters.Min(l => l.Location.Y);
            var maxY = letters.Max(l => l.Location.Y);
            var pageHeight = page.Height;

            _output.WriteLine($"\nY-axis range: {minY:F2} to {maxY:F2}");
            _output.WriteLine($"Page height: {pageHeight:F2}");

            // Typically headers are at top (high Y) and footers at bottom (low Y)
            var headerThreshold = maxY * 0.9; // Top 10%
            var footerThreshold = maxY * 0.1; // Bottom 10%

            var headerLetters = letters.Where(l => l.Location.Y >= headerThreshold).ToList();
            var footerLetters = letters.Where(l => l.Location.Y <= footerThreshold).ToList();
            var contentLetters = letters.Where(l => l.Location.Y < headerThreshold && l.Location.Y > footerThreshold).ToList();

            _output.WriteLine($"\nPotential header letters: {headerLetters.Count}");
            _output.WriteLine($"Potential content letters: {contentLetters.Count}");
            _output.WriteLine($"Potential footer letters: {footerLetters.Count}");
        }
    }
}
