using USCISFormTracker.Core;
using USCISFormTracker.Tests.TestHelpers;
using Xunit.Abstractions;

namespace USCISFormTracker.Tests;

/// <summary>
/// Easy-to-use tests for inspecting PDF extraction output
/// Just change the filename in the test to see what any PDF extracts to
/// </summary>
public class PdfInspectorTests
{
    private readonly ITestOutputHelper _output;

    public PdfInspectorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// QUICK INSPECTOR: Pass PDF_FILE environment variable or edit default below
    /// Usage: PDF_FILE=i-129f.pdf dotnet test --filter "InspectPdf_Quick"
    /// Uses ColumnAwarePdfReader
    /// </summary>
    [Fact]
    public void InspectPdf_Quick()
    {
        // Read from environment variable, or use default
        var filename = Environment.GetEnvironmentVariable("PDF_FILE") ?? "g-28.pdf";

        InspectPdfFile(filename);
    }

    /// <summary>
    /// IMPROVED (OLD) INSPECTOR: Pass PDF_FILE environment variable or edit default below
    /// Usage: PDF_FILE=i-129f.pdf dotnet test --filter "InspectPdf_Improved"
    /// Uses ImprovedPdfPigReader (legacy, without column detection)
    /// </summary>
    [Fact]
    public void InspectPdf_Improved()
    {
        // Read from environment variable, or use default
        var filename = Environment.GetEnvironmentVariable("PDF_FILE") ?? "g-28.pdf";

        InspectPdfFileWithImprovedReader(filename);
    }

    [Theory]
    [InlineData("PdfTest_First.pdf")]
    [InlineData("PdfTest_Second.pdf")]
    [InlineData("g-28.pdf")]
    [InlineData("g-28instr.pdf")]
    [InlineData("i-129f.pdf")]
    [InlineData("i-129finstr.pdf")]
    public void InspectAllPdfs(string filename)
    {
        InspectPdfFile(filename);
    }

    [Fact]
    public void ComparePdfReaders_OnRealForm()
    {
        // Read from environment variable, or use default
        var filename = Environment.GetEnvironmentVariable("PDF_FILE") ?? "g-28.pdf";

        var oldReader = new PdfPigReader();
        var newReader = new ImprovedPdfPigReader();
        var pdfBytes = TestDataLoader.LoadPdfFile(filename);

        using var stream1 = new MemoryStream(pdfBytes);
        var oldText = oldReader.GetPdfText(stream1);

        using var stream2 = new MemoryStream(pdfBytes);
        var newText = newReader.GetPdfText(stream2);

        _output.WriteLine($"=== FILE: {filename} ===\n");

        _output.WriteLine("=== OLD READER (PdfPigReader) ===");
        _output.WriteLine($"Length: {oldText.Length} chars, Lines: {oldText.Split('\n').Length}");
        _output.WriteLine("\n--- First 1000 chars ---");
        _output.WriteLine(oldText.Substring(0, Math.Min(1000, oldText.Length)));

        _output.WriteLine("\n\n=== NEW READER (ImprovedPdfPigReader) ===");
        _output.WriteLine($"Length: {newText.Length} chars, Lines: {newText.Split('\n').Length}");
        _output.WriteLine("\n--- First 1000 chars ---");
        _output.WriteLine(newText.Substring(0, Math.Min(1000, newText.Length)));

        _output.WriteLine("\n\n=== DIFFERENCE ===");
        _output.WriteLine($"Removed: {oldText.Length - newText.Length} chars");
        _output.WriteLine($"Reduction: {((oldText.Length - newText.Length) / (double)oldText.Length * 100):F1}%");
    }

    [Fact]
    public void CompareAllThreeReaders_OnRealForm()
    {
        // Read from environment variable, or use default
        var filename = Environment.GetEnvironmentVariable("PDF_FILE") ?? "g-28.pdf";

        var basicReader = new PdfPigReader();
        var improvedReader = new ImprovedPdfPigReader();
        var columnReader = new ColumnAwarePdfReader();
        var pdfBytes = TestDataLoader.LoadPdfFile(filename);

        using var stream1 = new MemoryStream(pdfBytes);
        var basicText = basicReader.GetPdfText(stream1);

        using var stream2 = new MemoryStream(pdfBytes);
        var improvedText = improvedReader.GetPdfText(stream2);

        using var stream3 = new MemoryStream(pdfBytes);
        var columnText = columnReader.GetPdfText(stream3);

        _output.WriteLine($"╔═══════════════════════════════════════════════════════════╗");
        _output.WriteLine($"║  READER COMPARISON: {filename,-37} ║");
        _output.WriteLine($"╚═══════════════════════════════════════════════════════════╝\n");

        _output.WriteLine("┌─────────────────────────────────────────────────────────┐");
        _output.WriteLine("│ 1. BASIC READER (PdfPigReader)                          │");
        _output.WriteLine("└─────────────────────────────────────────────────────────┘");
        _output.WriteLine($"Length: {basicText.Length:N0} chars, Lines: {basicText.Split('\n').Length}");
        _output.WriteLine("\n--- FULL TEXT ---");
        _output.WriteLine(basicText);
        _output.WriteLine(new string('─', 70));

        _output.WriteLine("\n┌─────────────────────────────────────────────────────────┐");
        _output.WriteLine("│ 2. IMPROVED READER (ImprovedPdfPigReader)               │");
        _output.WriteLine("└─────────────────────────────────────────────────────────┘");
        _output.WriteLine($"Length: {improvedText.Length:N0} chars, Lines: {improvedText.Split('\n').Length}");
        _output.WriteLine($"Reduction from Basic: {((basicText.Length - improvedText.Length) / (double)basicText.Length * 100):F1}%");
        _output.WriteLine("\n--- FULL TEXT ---");
        _output.WriteLine(improvedText);
        _output.WriteLine(new string('─', 70));

        _output.WriteLine("\n┌─────────────────────────────────────────────────────────┐");
        _output.WriteLine("│ 3. COLUMN-AWARE READER (ColumnAwarePdfReader)           │");
        _output.WriteLine("└─────────────────────────────────────────────────────────┘");
        _output.WriteLine($"Length: {columnText.Length:N0} chars, Lines: {columnText.Split('\n').Length}");
        _output.WriteLine($"Difference from Improved: {columnText.Length - improvedText.Length:+0;-0;0} chars");
        _output.WriteLine("\n--- FULL TEXT ---");
        _output.WriteLine(columnText);
        _output.WriteLine(new string('─', 70));

        _output.WriteLine("\n╔═══════════════════════════════════════════════════════════╗");
        _output.WriteLine("║  SUMMARY                                                  ║");
        _output.WriteLine("╚═══════════════════════════════════════════════════════════╝");
        _output.WriteLine($"Basic:        {basicText.Length,8:N0} chars");
        _output.WriteLine($"Improved:     {improvedText.Length,8:N0} chars (headers/footers removed)");
        _output.WriteLine($"Column-Aware: {columnText.Length,8:N0} chars (proper column ordering)");
    }

    private void InspectPdfFile(string filename)
    {
        var reader = new PdfPigLayoutPdfReader();
        var pdfBytes = TestDataLoader.LoadPdfFile(filename);

        using var stream = new MemoryStream(pdfBytes);
        var text = reader.GetPdfText(stream);

        var hasher = new Sha256Hasher();
        var hash = hasher.ComputeHash(text);

        _output.WriteLine($"╔═══════════════════════════════════════════════════════════╗");
        _output.WriteLine($"║  PDF EXTRACTION REPORT: {filename,-35} ║");
        _output.WriteLine($"║  Reader: PdfPigLayoutPdfReader (RecursiveXYCut)          ║");
        _output.WriteLine($"╚═══════════════════════════════════════════════════════════╝\n");

        _output.WriteLine($"File Size: {pdfBytes.Length:N0} bytes ({pdfBytes.Length / 1024.0:F1} KB)");
        _output.WriteLine($"Extracted Text Length: {text.Length:N0} characters");
        _output.WriteLine($"Line Count: {text.Split('\n').Length}");
        _output.WriteLine($"SHA256 Hash: {hash}");

        _output.WriteLine("\n" + new string('─', 70));
        _output.WriteLine("EXTRACTED TEXT:");
        _output.WriteLine(new string('─', 70) + "\n");
        _output.WriteLine(text);
        _output.WriteLine("\n" + new string('─', 70));
        _output.WriteLine("END OF EXTRACTION");
        _output.WriteLine(new string('─', 70));

        // Show first and last 200 chars for quick preview
        _output.WriteLine("\n=== QUICK PREVIEW ===");
        _output.WriteLine($"First 200 chars: {text.Substring(0, Math.Min(200, text.Length))}");
        if (text.Length > 200)
        {
            _output.WriteLine($"\n... ({text.Length - 400} chars omitted) ...\n");
            _output.WriteLine($"Last 200 chars: {text.Substring(Math.Max(0, text.Length - 200))}");
        }
    }

    private void InspectPdfFileWithImprovedReader(string filename)
    {
        var reader = new ImprovedPdfPigReader();
        var pdfBytes = TestDataLoader.LoadPdfFile(filename);

        using var stream = new MemoryStream(pdfBytes);
        var text = reader.GetPdfText(stream);

        var hasher = new Sha256Hasher();
        var hash = hasher.ComputeHash(text);

        _output.WriteLine($"╔═══════════════════════════════════════════════════════════╗");
        _output.WriteLine($"║  PDF EXTRACTION REPORT: {filename,-35} ║");
        _output.WriteLine($"║  Reader: ImprovedPdfPigReader (Legacy)                   ║");
        _output.WriteLine($"╚═══════════════════════════════════════════════════════════╝\n");

        _output.WriteLine($"File Size: {pdfBytes.Length:N0} bytes ({pdfBytes.Length / 1024.0:F1} KB)");
        _output.WriteLine($"Extracted Text Length: {text.Length:N0} characters");
        _output.WriteLine($"Line Count: {text.Split('\n').Length}");
        _output.WriteLine($"SHA256 Hash: {hash}");

        _output.WriteLine("\n" + new string('─', 70));
        _output.WriteLine("EXTRACTED TEXT:");
        _output.WriteLine(new string('─', 70) + "\n");
        _output.WriteLine(text);
        _output.WriteLine("\n" + new string('─', 70));
        _output.WriteLine("END OF EXTRACTION");
        _output.WriteLine(new string('─', 70));

        // Show first and last 200 chars for quick preview
        _output.WriteLine("\n=== QUICK PREVIEW ===");
        _output.WriteLine($"First 200 chars: {text.Substring(0, Math.Min(200, text.Length))}");
        if (text.Length > 200)
        {
            _output.WriteLine($"\n... ({text.Length - 400} chars omitted) ...\n");
            _output.WriteLine($"Last 200 chars: {text.Substring(Math.Max(0, text.Length - 200))}");
        }
    }

    [Fact]
    public void ShowAllAvailablePdfs()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var pdfDir = Path.Combine(baseDir, "TestData", "Pdf");
        var pdfs = Directory.GetFiles(pdfDir, "*.pdf");

        _output.WriteLine("═══════════════════════════════════════");
        _output.WriteLine("  AVAILABLE PDFs FOR INSPECTION");
        _output.WriteLine("═══════════════════════════════════════\n");

        foreach (var pdf in pdfs.OrderBy(p => p))
        {
            var filename = Path.GetFileName(pdf);
            var fileInfo = new FileInfo(pdf);
            _output.WriteLine($"  • {filename,-30} ({fileInfo.Length / 1024.0:F1} KB)");
        }

        _output.WriteLine($"\nTotal: {pdfs.Length} PDFs");
        _output.WriteLine("\nTo inspect a PDF, edit InspectPdf_Quick() test");
        _output.WriteLine("and change the filename variable.");
    }

    /// <summary>
    /// COMPARISON: Compare ColumnAwarePdfReader vs PdfPigLayoutPdfReader
    /// Usage: PDF_FILE=g-28.pdf dotnet test --filter "CompareCustomVsDocstrum"
    /// </summary>
    [Fact]
    public void CompareCustomVsDocstrum()
    {
        // Read from environment variable, or use default
        var filename = Environment.GetEnvironmentVariable("PDF_FILE") ?? "g-28.pdf";

        var customReader = new ColumnAwarePdfReader();
        var docstrumReader = new PdfPigLayoutPdfReader();
        var pdfBytes = TestDataLoader.LoadPdfFile(filename);

        using var stream1 = new MemoryStream(pdfBytes);
        var customText = customReader.GetPdfText(stream1);

        using var stream2 = new MemoryStream(pdfBytes);
        var docstrumText = docstrumReader.GetPdfText(stream2);

        _output.WriteLine($"╔═══════════════════════════════════════════════════════════╗");
        _output.WriteLine($"║  READER COMPARISON: {filename,-37} ║");
        _output.WriteLine($"╚═══════════════════════════════════════════════════════════╝\n");

        _output.WriteLine("┌─────────────────────────────────────────────────────────┐");
        _output.WriteLine("│ CUSTOM READER (ColumnAwarePdfReader)                    │");
        _output.WriteLine("│ Uses Y-Level Gap Analysis with hardcoded thresholds     │");
        _output.WriteLine("└─────────────────────────────────────────────────────────┘");
        _output.WriteLine($"Length: {customText.Length:N0} chars, Lines: {customText.Split('\n').Length}");
        _output.WriteLine("\n--- FIRST 800 CHARS ---");
        _output.WriteLine(customText.Substring(0, Math.Min(800, customText.Length)));
        _output.WriteLine(new string('─', 70));

        _output.WriteLine("\n┌─────────────────────────────────────────────────────────┐");
        _output.WriteLine("│ DOCSTRUM READER (PdfPigLayoutPdfReader)                 │");
        _output.WriteLine("│ Uses PdfPig's Docstrum algorithm with reading order     │");
        _output.WriteLine("└─────────────────────────────────────────────────────────┘");
        _output.WriteLine($"Length: {docstrumText.Length:N0} chars, Lines: {docstrumText.Split('\n').Length}");
        _output.WriteLine("\n--- FIRST 800 CHARS ---");
        _output.WriteLine(docstrumText.Substring(0, Math.Min(800, docstrumText.Length)));
        _output.WriteLine(new string('─', 70));

        _output.WriteLine("\n╔═══════════════════════════════════════════════════════════╗");
        _output.WriteLine("║  ANALYSIS                                                 ║");
        _output.WriteLine("╚═══════════════════════════════════════════════════════════╝");
        _output.WriteLine($"Length difference: {docstrumText.Length - customText.Length:+0;-0;0} chars");
        _output.WriteLine($"Line difference: {docstrumText.Split('\n').Length - customText.Split('\n').Length:+0;-0;0} lines");

        var identical = customText == docstrumText;
        _output.WriteLine($"Texts identical: {identical}");

        if (!identical)
        {
            // Find first difference
            var minLen = Math.Min(customText.Length, docstrumText.Length);
            for (int i = 0; i < minLen; i++)
            {
                if (customText[i] != docstrumText[i])
                {
                    _output.WriteLine($"\nFirst difference at position {i}:");
                    var start = Math.Max(0, i - 50);
                    var end = Math.Min(minLen, i + 50);
                    _output.WriteLine($"Custom:   ...{customText.Substring(start, end - start)}...");
                    _output.WriteLine($"Docstrum: ...{docstrumText.Substring(start, end - start)}...");
                    break;
                }
            }
        }
    }

    /// <summary>
    /// DIAGNOSTIC: Shows column detection details for debugging
    /// Usage: PDF_FILE=g-28.pdf dotnet test --filter "InspectPdfColumnDetection"
    /// </summary>
    [Fact]
    public void InspectPdfColumnDetection()
    {
        // Read from environment variable, or use default
        var filename = Environment.GetEnvironmentVariable("PDF_FILE") ?? "g-28.pdf";
        var pdfBytes = TestDataLoader.LoadPdfFile(filename);

        using var document = UglyToad.PdfPig.PdfDocument.Open(new MemoryStream(pdfBytes));
        var page = document.GetPage(1);
        var words = page.GetWords().ToList();

        _output.WriteLine($"╔═══════════════════════════════════════════════════════════╗");
        _output.WriteLine($"║  COLUMN DETECTION DIAGNOSTIC: {filename,-31} ║");
        _output.WriteLine($"╚═══════════════════════════════════════════════════════════╝\n");

        _output.WriteLine($"Page Width: {page.Width:F1} points");
        _output.WriteLine($"Page Height: {page.Height:F1} points");
        _output.WriteLine($"Total Words: {words.Count}");

        // Show first 30 words with coordinates
        _output.WriteLine("\n=== FIRST 30 WORDS (with X,Y coordinates) ===");
        foreach (var word in words.Take(30))
        {
            _output.WriteLine($"  \"{word.Text,-20}\" X: {word.BoundingBox.Left,6:F1}  Y: {word.BoundingBox.Bottom,6:F1}");
        }

        // Analyze gaps on first few lines manually
        _output.WriteLine("\n=== GAP ANALYSIS (First 5 Lines) ===");
        var sortedByY = words.OrderByDescending(w => w.BoundingBox.Bottom).ToList();
        var lines = new List<List<UglyToad.PdfPig.Content.Word>>();
        List<UglyToad.PdfPig.Content.Word>? currentLine = null;
        double? currentY = null;

        foreach (var word in sortedByY)
        {
            var wordY = word.BoundingBox.Bottom;
            if (currentY == null || Math.Abs(wordY - currentY.Value) > 3.0)
            {
                currentLine = new List<UglyToad.PdfPig.Content.Word> { word };
                lines.Add(currentLine);
                currentY = wordY;
            }
            else
            {
                currentLine!.Add(word);
            }
        }

        for (int i = 0; i < Math.Min(5, lines.Count); i++)
        {
            var line = lines[i].OrderBy(w => w.BoundingBox.Left).ToList();
            _output.WriteLine($"\nLine {i + 1} (Y~{line[0].BoundingBox.Bottom:F1}): {line.Count} words");

            for (int j = 0; j < line.Count - 1; j++)
            {
                var gap = line[j + 1].BoundingBox.Left - line[j].BoundingBox.Right;
                if (gap > 50)
                {
                    _output.WriteLine($"  LARGE GAP: \"{line[j].Text}\" to \"{line[j + 1].Text}\" = {gap:F1} points");
                }
            }
        }

        // Now run the column-aware reader and show results
        _output.WriteLine("\n=== COLUMN DETECTION RESULTS ===");
        var reader = new ColumnAwarePdfReader();
        using var stream = new MemoryStream(pdfBytes);
        var extractedText = reader.GetPdfText(stream);

        _output.WriteLine($"\nExtracted Text Length: {extractedText.Length} chars");
        _output.WriteLine($"Extracted Line Count: {extractedText.Split('\n').Length}");

        _output.WriteLine("\n=== FIRST 500 CHARS OF EXTRACTED TEXT ===");
        _output.WriteLine(extractedText.Substring(0, Math.Min(500, extractedText.Length)));
    }
}
