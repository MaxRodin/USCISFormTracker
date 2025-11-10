using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;
using USCISFormTracker.Tests.TestHelpers;
using Xunit.Abstractions;

namespace USCISFormTracker.Tests;

/// <summary>
/// Diagnostic tests to understand what RecursiveXYCut is doing with g-28.pdf
/// Shows blocks, bounding boxes, and ordering to debug column separation issues
/// </summary>
public class RecursiveXYCutDiagnosticTests
{
    private readonly ITestOutputHelper _output;

    public RecursiveXYCutDiagnosticTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void DiagnoseRecursiveXYCut_G28()
    {
        var filename = "g-28.pdf";
        var pdfBytes = TestDataLoader.LoadPdfFile(filename);

        using var document = PdfDocument.Open(new MemoryStream(pdfBytes));
        var page = document.GetPage(1);

        _output.WriteLine($"╔═══════════════════════════════════════════════════════════╗");
        _output.WriteLine($"║  RecursiveXYCut DIAGNOSTIC: {filename,-35} ║");
        _output.WriteLine($"╚═══════════════════════════════════════════════════════════╝\n");

        _output.WriteLine($"Page Width: {page.Width:F1} points");
        _output.WriteLine($"Page Height: {page.Height:F1} points");

        // Step 1: Extract words using NearestNeighbour (same as PdfPigLayoutPdfReader)
        var allWords = page.GetWords(NearestNeighbourWordExtractor.Instance).ToList();
        _output.WriteLine($"Total Words (NearestNeighbour): {allWords.Count}");

        // Step 2: Filter footers (same as PdfPigLayoutPdfReader)
        var footerMargin = 80.0;
        var contentWords = allWords.Where(w => w.BoundingBox.Bottom > footerMargin).ToList();
        _output.WriteLine($"Words after footer filter: {contentWords.Count}");
        _output.WriteLine($"Footer threshold: {footerMargin} points\n");

        // Step 3: Run RecursiveXYCut
        var textBlocks = RecursiveXYCut.Instance.GetBlocks(contentWords);
        _output.WriteLine($"╔═══════════════════════════════════════════════════════════╗");
        _output.WriteLine($"║  RecursiveXYCut created {textBlocks.Count} blocks");
        _output.WriteLine($"╚═══════════════════════════════════════════════════════════╝\n");

        // Show detailed information about each block
        int blockNum = 1;
        foreach (var block in textBlocks)
        {
            var bbox = block.BoundingBox;
            _output.WriteLine($"┌── BLOCK {blockNum} ──────────────────────────────────────────────┐");
            _output.WriteLine($"│ Bounding Box:                                             │");
            _output.WriteLine($"│   Left:   {bbox.Left,7:F1}  Top:    {bbox.Top,7:F1}                    │");
            _output.WriteLine($"│   Right:  {bbox.Right,7:F1}  Bottom: {bbox.Bottom,7:F1}                    │");
            _output.WriteLine($"│   Width:  {bbox.Width,7:F1}  Height: {bbox.Height,7:F1}                    │");
            _output.WriteLine($"│                                                           │");
            _output.WriteLine($"│ Text Length: {block.Text.Length} chars                                   │");
            _output.WriteLine($"│ Text Lines: {block.Text.Split('\n').Length}                                     │");
            _output.WriteLine($"└───────────────────────────────────────────────────────────┘");
            
            // Show first 300 chars of block text
            var preview = block.Text.Length > 300 
                ? block.Text.Substring(0, 300) + "..." 
                : block.Text;
            _output.WriteLine("Text Preview:");
            _output.WriteLine(preview);
            _output.WriteLine(new string('─', 70) + "\n");
            
            blockNum++;
        }

        // Step 4: Show the ordering that PdfPigLayoutPdfReader uses
        _output.WriteLine($"╔═══════════════════════════════════════════════════════════╗");
        _output.WriteLine($"║  BLOCK ORDERING (Top→Bottom, Left→Right)                 ║");
        _output.WriteLine($"╚═══════════════════════════════════════════════════════════╝\n");

        var orderedBlocks = textBlocks
            .OrderByDescending(b => b.BoundingBox.Top)
            .ThenBy(b => b.BoundingBox.Left)
            .ToList();

        for (int i = 0; i < orderedBlocks.Count; i++)
        {
            var block = orderedBlocks[i];
            var firstLine = block.Text.Split('\n')[0];
            if (firstLine.Length > 60)
                firstLine = firstLine.Substring(0, 60) + "...";

            _output.WriteLine($"{i + 1}. Top={block.BoundingBox.Top,6:F1}, Left={block.BoundingBox.Left,6:F1}");
            _output.WriteLine($"   \"{firstLine}\"");
            _output.WriteLine("");
        }

        // Step 5: Show the final combined text (what PdfPigLayoutPdfReader returns)
        _output.WriteLine($"╔═══════════════════════════════════════════════════════════╗");
        _output.WriteLine($"║  FINAL COMBINED TEXT (First 800 chars)                   ║");
        _output.WriteLine($"╚═══════════════════════════════════════════════════════════╝\n");

        var combinedText = string.Join("\n", orderedBlocks.Select(b => b.Text));
        var finalPreview = combinedText.Length > 800 
            ? combinedText.Substring(0, 800) + "..." 
            : combinedText;
        _output.WriteLine(finalPreview);

        // Step 6: Analyze if blocks represent columns
        _output.WriteLine($"\n╔═══════════════════════════════════════════════════════════╗");
        _output.WriteLine($"║  COLUMN ANALYSIS                                          ║");
        _output.WriteLine($"╚═══════════════════════════════════════════════════════════╝\n");

        var pageCenter = page.Width / 2;
        var leftBlocks = textBlocks.Where(b => b.BoundingBox.Left < pageCenter).ToList();
        var rightBlocks = textBlocks.Where(b => b.BoundingBox.Left >= pageCenter).ToList();

        _output.WriteLine($"Page center X: {pageCenter:F1}");
        _output.WriteLine($"Blocks in left half: {leftBlocks.Count}");
        _output.WriteLine($"Blocks in right half: {rightBlocks.Count}");

        if (textBlocks.Count == 1)
        {
            _output.WriteLine("\n⚠️  WARNING: Only 1 block detected!");
            _output.WriteLine("    RecursiveXYCut is NOT separating columns.");
            _output.WriteLine("    This is why left/right column text is merged.");
        }
        else if (textBlocks.Count > 10)
        {
            _output.WriteLine("\n⚠️  WARNING: Too many blocks detected!");
            _output.WriteLine("    RecursiveXYCut may be over-segmenting.");
        }
        else
        {
            _output.WriteLine("\n✓ Multiple blocks detected.");
            _output.WriteLine("  Check if blocks align with expected columns.");
        }
    }

    [Fact]
    public void CompareRecursiveXYCutWithColumnAware_G28()
    {
        var filename = "g-28.pdf";
        var pdfBytes = TestDataLoader.LoadPdfFile(filename);

        using var document = PdfDocument.Open(new MemoryStream(pdfBytes));
        var page = document.GetPage(1);

        // RecursiveXYCut approach (PdfPigLayoutPdfReader)
        var allWords = page.GetWords(NearestNeighbourWordExtractor.Instance).ToList();
        var contentWords = allWords.Where(w => w.BoundingBox.Bottom > 80.0).ToList();
        var xyBlocks = RecursiveXYCut.Instance.GetBlocks(contentWords);

        // ColumnAware approach (custom gap analysis)
        var columnReader = new USCISFormTracker.Core.ColumnAwarePdfReader();
        using var stream = new MemoryStream(pdfBytes);
        var columnText = columnReader.GetPdfText(stream);

        _output.WriteLine($"╔═══════════════════════════════════════════════════════════╗");
        _output.WriteLine($"║  COMPARISON: RecursiveXYCut vs ColumnAware               ║");
        _output.WriteLine($"╚═══════════════════════════════════════════════════════════╝\n");

        _output.WriteLine("┌─────────────────────────────────────────────────────────┐");
        _output.WriteLine("│ RecursiveXYCut                                          │");
        _output.WriteLine("└─────────────────────────────────────────────────────────┘");
        _output.WriteLine($"Blocks created: {xyBlocks.Count}");
        
        var xyText = string.Join("\n", xyBlocks
            .OrderByDescending(b => b.BoundingBox.Top)
            .ThenBy(b => b.BoundingBox.Left)
            .Select(b => b.Text));
        
        _output.WriteLine($"Text length: {xyText.Length} chars");
        _output.WriteLine($"First 400 chars:");
        _output.WriteLine(xyText.Substring(0, Math.Min(400, xyText.Length)));
        _output.WriteLine(new string('─', 70));

        _output.WriteLine("\n┌─────────────────────────────────────────────────────────┐");
        _output.WriteLine("│ ColumnAwarePdfReader                                    │");
        _output.WriteLine("└─────────────────────────────────────────────────────────┘");
        _output.WriteLine($"Text length: {columnText.Length} chars");
        _output.WriteLine($"First 400 chars:");
        _output.WriteLine(columnText.Substring(0, Math.Min(400, columnText.Length)));
        _output.WriteLine(new string('─', 70));

        _output.WriteLine("\n╔═══════════════════════════════════════════════════════════╗");
        _output.WriteLine("║  VERDICT                                                  ║");
        _output.WriteLine("╚═══════════════════════════════════════════════════════════╝");
        
        if (xyText == columnText)
        {
            _output.WriteLine("✓ Both produce identical output!");
        }
        else
        {
            _output.WriteLine("✗ Outputs differ!");
            _output.WriteLine($"\nLength difference: {xyText.Length - columnText.Length:+0;-0;0} chars");
            
            // Find first difference
            var minLen = Math.Min(xyText.Length, columnText.Length);
            for (int i = 0; i < minLen; i++)
            {
                if (xyText[i] != columnText[i])
                {
                    _output.WriteLine($"\nFirst difference at position {i}:");
                    var start = Math.Max(0, i - 40);
                    var end = Math.Min(minLen, i + 80);
                    _output.WriteLine($"XYCut:  ...{xyText.Substring(start, end - start)}...");
                    _output.WriteLine($"Column: ...{columnText.Substring(start, end - start)}...");
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Check if RecursiveXYCut has any configuration options we can tune
    /// </summary>
    [Fact]
    public void InvestigateRecursiveXYCutOptions()
    {
        _output.WriteLine("╔═══════════════════════════════════════════════════════════╗");
        _output.WriteLine("║  RecursiveXYCut Configuration Investigation              ║");
        _output.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

        var instance = RecursiveXYCut.Instance;
        var type = instance.GetType();

        _output.WriteLine($"Type: {type.FullName}");
        _output.WriteLine($"\nPublic Properties:");
        
        var props = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (props.Length == 0)
        {
            _output.WriteLine("  (none)");
        }
        else
        {
            foreach (var prop in props)
            {
                _output.WriteLine($"  - {prop.Name}: {prop.PropertyType.Name}");
            }
        }

        _output.WriteLine($"\nPublic Fields:");
        var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (fields.Length == 0)
        {
            _output.WriteLine("  (none)");
        }
        else
        {
            foreach (var field in fields)
            {
                _output.WriteLine($"  - {field.Name}: {field.FieldType.Name}");
            }
        }

        _output.WriteLine($"\nPublic Methods:");
        var methods = type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(m => !m.IsSpecialName && m.DeclaringType == type);
        
        foreach (var method in methods)
        {
            var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
            _output.WriteLine($"  - {method.Name}({parameters}): {method.ReturnType.Name}");
        }

        _output.WriteLine("\n" + new string('─', 70));
        _output.WriteLine("CONCLUSION:");
        _output.WriteLine("If no tunable parameters exist, RecursiveXYCut cannot be configured.");
        _output.WriteLine("Recommendation: Use ColumnAwarePdfReader if it produces better results.");
    }
}
