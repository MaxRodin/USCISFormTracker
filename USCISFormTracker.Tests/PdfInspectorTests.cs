using USCISFormTracker.Core;
using USCISFormTracker.Core.PdfReaders;
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
    /// Uses PdfPigLayoutPdfReader
    /// </summary>
    [Fact]
    public void InspectPdf_Quick()
    {
        // Read from environment variable, or use default
        var filename = Environment.GetEnvironmentVariable("PDF_FILE") ?? "g-28.pdf";

        InspectPdfFile(filename);
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


}
