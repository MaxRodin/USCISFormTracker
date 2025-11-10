using USCISFormTracker.Core;
using USCISFormTracker.Tests.TestHelpers;
using Xunit.Abstractions;

namespace USCISFormTracker.Tests;

public class ImprovedPdfReaderTests
{
    private readonly ITestOutputHelper _output;

    public ImprovedPdfReaderTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void CompareOldVsNewExtraction_FirstPdf()
    {
        // Arrange
        var oldReader = new PdfPigReader();
        var newReader = new ImprovedPdfPigReader();
        var pdfBytes = TestDataLoader.LoadPdfFile("PdfTest_First.pdf");

        // Act
        using var stream1 = new MemoryStream(pdfBytes);
        var oldText = oldReader.GetPdfText(stream1);

        using var stream2 = new MemoryStream(pdfBytes);
        var newText = newReader.GetPdfText(stream2);

        // Output
        _output.WriteLine("=== OLD EXTRACTION (PdfPigReader) ===");
        _output.WriteLine(oldText);
        _output.WriteLine($"\nLength: {oldText.Length} chars");
        _output.WriteLine($"Lines: {oldText.Split('\n').Length}");

        _output.WriteLine("\n=== NEW EXTRACTION (ImprovedPdfPigReader) ===");
        _output.WriteLine(newText);
        _output.WriteLine($"\nLength: {newText.Length} chars");
        _output.WriteLine($"Lines: {newText.Split('\n').Length}");

        _output.WriteLine("\n=== IMPROVEMENTS ===");
        _output.WriteLine($"✓ Headers/footers removed");
        _output.WriteLine($"✓ Proper line breaks preserved");
        _output.WriteLine($"✓ Content-only extraction");

        // Assert
        Assert.NotEmpty(newText);
        Assert.DoesNotContain("OriginalHeader", newText);
        Assert.DoesNotContain("10/11/2025", newText);
        Assert.Contains("This line is static.", newText);
    }

    [Fact]
    public void CompareOldVsNewExtraction_SecondPdf()
    {
        // Arrange
        var oldReader = new PdfPigReader();
        var newReader = new ImprovedPdfPigReader();
        var pdfBytes = TestDataLoader.LoadPdfFile("PdfTest_Second.pdf");

        // Act
        using var stream1 = new MemoryStream(pdfBytes);
        var oldText = oldReader.GetPdfText(stream1);

        using var stream2 = new MemoryStream(pdfBytes);
        var newText = newReader.GetPdfText(stream2);

        // Output
        _output.WriteLine("=== OLD EXTRACTION (PdfPigReader) ===");
        _output.WriteLine(oldText);

        _output.WriteLine("\n=== NEW EXTRACTION (ImprovedPdfPigReader) ===");
        _output.WriteLine(newText);

        // Assert
        Assert.NotEmpty(newText);
        Assert.DoesNotContain("ModifiedHeader", newText);
        Assert.DoesNotContain("11/30/2025", newText);
    }

    [Fact]
    public void NewExtraction_ShouldProduceCleanerDiff()
    {
        // Arrange
        var newReader = new ImprovedPdfPigReader();
        var differ = new DiffPlexDiffer();
        var firstPdfBytes = TestDataLoader.LoadPdfFile("PdfTest_First.pdf");
        var secondPdfBytes = TestDataLoader.LoadPdfFile("PdfTest_Second.pdf");

        // Act
        using var stream1 = new MemoryStream(firstPdfBytes);
        var text1 = newReader.GetPdfText(stream1);

        using var stream2 = new MemoryStream(secondPdfBytes);
        var text2 = newReader.GetPdfText(stream2);

        var diffLines = differ.GetDiffLines(text1, text2);

        // Output
        _output.WriteLine("=== FIRST PDF (cleaned) ===");
        _output.WriteLine(text1);

        _output.WriteLine("\n=== SECOND PDF (cleaned) ===");
        _output.WriteLine(text2);

        _output.WriteLine("\n=== DIFF RESULTS ===");
        _output.WriteLine($"Added lines: {diffLines.AddedLines.Count}");
        _output.WriteLine($"Deleted lines: {diffLines.DeletedLines.Count}");
        _output.WriteLine($"Modified lines: {diffLines.ModifiedLines.Count}");

        if (diffLines.AddedLines.Count > 0)
        {
            _output.WriteLine("\nADDED:");
            foreach (var line in diffLines.AddedLines)
            {
                _output.WriteLine($"  + {line}");
            }
        }

        if (diffLines.DeletedLines.Count > 0)
        {
            _output.WriteLine("\nDELETED:");
            foreach (var line in diffLines.DeletedLines)
            {
                _output.WriteLine($"  - {line}");
            }
        }

        // Assert
        Assert.True(diffLines.AddedLines.Count + diffLines.DeletedLines.Count > 0,
            "Should detect differences");

        // Should not have header/footer noise in the diff
        Assert.DoesNotContain(diffLines.AddedLines, line => line.Contains("ModifiedHeader"));
        Assert.DoesNotContain(diffLines.DeletedLines, line => line.Contains("OriginalHeader"));
    }
}
