using USCISFormTracker.Core;
using USCISFormTracker.Core.PdfReaders;
using USCISFormTracker.Tests.TestHelpers;
using Xunit.Abstractions;

namespace USCISFormTracker.Tests;

/// <summary>
/// Tests specifically for inspecting the DiffLines output
/// </summary>
public class DiffInspectionTests
{
    private readonly ITestOutputHelper _output;

    public DiffInspectionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void InspectDiffLines_BetweenTwoPdfVersions()
    {
        // Arrange
        var pdfReader = new PdfPigLayoutPdfReader();
        var differ = new DiffPlexDiffer();
        var firstPdfBytes = TestDataLoader.LoadPdfFile("PdfTest_First.pdf");
        var secondPdfBytes = TestDataLoader.LoadPdfFile("PdfTest_Second.pdf");

        // Act - Extract text from both PDFs
        using var stream1 = new MemoryStream(firstPdfBytes);
        var text1 = pdfReader.GetPdfText(stream1);

        using var stream2 = new MemoryStream(secondPdfBytes);
        var text2 = pdfReader.GetPdfText(stream2);

        // Compute diff
        var diffLines = differ.GetDiffLines(text1, text2);

        // Output detailed inspection
        _output.WriteLine("=== PDF TEXT COMPARISON ===");
        _output.WriteLine($"\nFirst PDF length: {text1.Length} characters");
        _output.WriteLine($"Second PDF length: {text2.Length} characters");
        _output.WriteLine($"\n=== DIFF SUMMARY ===");
        _output.WriteLine($"Added lines: {diffLines.AddedLines.Count}");
        _output.WriteLine($"Deleted lines: {diffLines.DeletedLines.Count}");
        _output.WriteLine($"Modified lines: {diffLines.ModifiedLines.Count}");

        if (diffLines.AddedLines.Count > 0)
        {
            _output.WriteLine("\n=== ADDED LINES ===");
            foreach (var line in diffLines.AddedLines.Take(20)) // Show first 20
            {
                _output.WriteLine($"  + {line}");
            }
            if (diffLines.AddedLines.Count > 20)
            {
                _output.WriteLine($"  ... and {diffLines.AddedLines.Count - 20} more");
            }
        }

        if (diffLines.DeletedLines.Count > 0)
        {
            _output.WriteLine("\n=== DELETED LINES ===");
            foreach (var line in diffLines.DeletedLines.Take(20)) // Show first 20
            {
                _output.WriteLine($"  - {line}");
            }
            if (diffLines.DeletedLines.Count > 20)
            {
                _output.WriteLine($"  ... and {diffLines.DeletedLines.Count - 20} more");
            }
        }

        if (diffLines.ModifiedLines.Count > 0)
        {
            _output.WriteLine("\n=== MODIFIED LINES ===");
            foreach (var line in diffLines.ModifiedLines.Take(20)) // Show first 20
            {
                _output.WriteLine($"  ~ {line}");
            }
            if (diffLines.ModifiedLines.Count > 20)
            {
                _output.WriteLine($"  ... and {diffLines.ModifiedLines.Count - 20} more");
            }
        }

        // Assert
        var totalChanges = diffLines.AddedLines.Count + diffLines.DeletedLines.Count + diffLines.ModifiedLines.Count;
        Assert.True(totalChanges > 0, "Should detect differences between the two PDF versions");
    }

    [Fact]
    public void InspectPdfText_FirstVersion()
    {
        // Arrange
        var pdfReader = new PdfPigLayoutPdfReader();
        var pdfBytes = TestDataLoader.LoadPdfFile("PdfTest_First.pdf");

        // Act
        using var stream = new MemoryStream(pdfBytes);
        var text = pdfReader.GetPdfText(stream);

        // Output
        _output.WriteLine("=== FIRST PDF TEXT CONTENT ===");
        _output.WriteLine($"Total length: {text.Length} characters");
        _output.WriteLine($"Total lines: {text.Split('\n').Length}");
        _output.WriteLine("\n--- First 2000 characters ---");
        _output.WriteLine(text.Substring(0, Math.Min(2000, text.Length)));

        // Assert
        Assert.NotEmpty(text);
    }

    [Fact]
    public void InspectPdfText_SecondVersion()
    {
        // Arrange
        var pdfReader = new PdfPigLayoutPdfReader();
        var pdfBytes = TestDataLoader.LoadPdfFile("PdfTest_Second.pdf");

        // Act
        using var stream = new MemoryStream(pdfBytes);
        var text = pdfReader.GetPdfText(stream);

        // Output
        _output.WriteLine("=== SECOND PDF TEXT CONTENT ===");
        _output.WriteLine($"Total length: {text.Length} characters");
        _output.WriteLine($"Total lines: {text.Split('\n').Length}");
        _output.WriteLine("\n--- First 2000 characters ---");
        _output.WriteLine(text.Substring(0, Math.Min(2000, text.Length)));

        // Assert
        Assert.NotEmpty(text);
    }
}
