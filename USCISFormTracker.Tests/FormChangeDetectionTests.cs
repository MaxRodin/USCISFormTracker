using USCISFormTracker.Core;
using USCISFormTracker.Core.Models;
using USCISFormTracker.Core.PdfReaders;
using USCISFormTracker.Data;
using USCISFormTracker.Tests.TestHelpers;

namespace USCISFormTracker.Tests;

/// <summary>
/// Tests for end-to-end form change detection workflow
/// </summary>
public class FormChangeDetectionTests
{
    [Fact]
    public void PdfReader_ShouldExtractTextFromPdf()
    {
        // Arrange
        var pdfReader = new PdfPigLayoutPdfReader();
        var pdfBytes = TestDataLoader.LoadPdfFile("PdfTest_First.pdf");

        // Act
        using var stream = new MemoryStream(pdfBytes);
        var text = pdfReader.GetPdfText(stream);

        // Assert
        Assert.NotNull(text);
        Assert.NotEmpty(text);
    }

    [Fact]
    public void Hasher_ShouldComputeConsistentHash_ForSamePdf()
    {
        // Arrange
        var hasher = new Sha256Hasher();
        var pdfReader = new PdfPigLayoutPdfReader();
        var pdfBytes = TestDataLoader.LoadPdfFile("PdfTest_First.pdf");

        // Act
        using var stream1 = new MemoryStream(pdfBytes);
        var text1 = pdfReader.GetPdfText(stream1);
        var hash1 = hasher.ComputeHash(text1);

        using var stream2 = new MemoryStream(pdfBytes);
        var text2 = pdfReader.GetPdfText(stream2);
        var hash2 = hasher.ComputeHash(text2);

        // Assert
        Assert.Equal(hash1, hash2);
        Assert.NotEmpty(hash1);
    }

    [Fact]
    public void Hasher_ShouldComputeDifferentHashes_ForDifferentPdfs()
    {
        // Arrange
        var hasher = new Sha256Hasher();
        var pdfReader = new PdfPigLayoutPdfReader();
        var firstPdfBytes = TestDataLoader.LoadPdfFile("PdfTest_First.pdf");
        var secondPdfBytes = TestDataLoader.LoadPdfFile("PdfTest_Second.pdf");

        // Act
        using var stream1 = new MemoryStream(firstPdfBytes);
        var text1 = pdfReader.GetPdfText(stream1);
        var hash1 = hasher.ComputeHash(text1);

        using var stream2 = new MemoryStream(secondPdfBytes);
        var text2 = pdfReader.GetPdfText(stream2);
        var hash2 = hasher.ComputeHash(text2);

        // Assert
        Assert.NotEqual(hash1, hash2);
        Assert.NotEmpty(hash1);
        Assert.NotEmpty(hash2);
    }

    [Fact]
    public void Differ_ShouldDetectChanges_BetweenTwoPdfVersions()
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

        // Assert - Should detect some differences
        var totalChanges = diffLines.AddedLines.Count + diffLines.DeletedLines.Count + diffLines.ModifiedLines.Count;
        Assert.True(totalChanges > 0, "Expected to find changes between the two PDF versions");
    }

    [Fact]
    public void Differ_ShouldProvideDetailedDiffLines_ForInspection()
    {
        // Arrange
        var pdfReader = new PdfPigLayoutPdfReader();
        var differ = new DiffPlexDiffer();
        var firstPdfBytes = TestDataLoader.LoadPdfFile("PdfTest_First.pdf");
        var secondPdfBytes = TestDataLoader.LoadPdfFile("PdfTest_Second.pdf");

        // Act
        using var stream1 = new MemoryStream(firstPdfBytes);
        var text1 = pdfReader.GetPdfText(stream1);

        using var stream2 = new MemoryStream(secondPdfBytes);
        var text2 = pdfReader.GetPdfText(stream2);

        var diffLines = differ.GetDiffLines(text1, text2);

        // Assert and inspect the diff
        Assert.NotNull(diffLines);
        Assert.NotNull(diffLines.AddedLines);
        Assert.NotNull(diffLines.DeletedLines);
        Assert.NotNull(diffLines.ModifiedLines);

        // Output for inspection during test runs
        if (diffLines.AddedLines.Count > 0)
        {
            System.Diagnostics.Debug.WriteLine("=== ADDED LINES ===");
            foreach (var line in diffLines.AddedLines)
            {
                System.Diagnostics.Debug.WriteLine($"  + {line}");
            }
        }

        if (diffLines.DeletedLines.Count > 0)
        {
            System.Diagnostics.Debug.WriteLine("=== DELETED LINES ===");
            foreach (var line in diffLines.DeletedLines)
            {
                System.Diagnostics.Debug.WriteLine($"  - {line}");
            }
        }

        if (diffLines.ModifiedLines.Count > 0)
        {
            System.Diagnostics.Debug.WriteLine("=== MODIFIED LINES ===");
            foreach (var line in diffLines.ModifiedLines)
            {
                System.Diagnostics.Debug.WriteLine($"  ~ {line}");
            }
        }

        System.Diagnostics.Debug.WriteLine($"\nTotal changes: Added={diffLines.AddedLines.Count}, Deleted={diffLines.DeletedLines.Count}, Modified={diffLines.ModifiedLines.Count}");
    }

    [Fact]
    public async Task FullWorkflow_ShouldDetectFormChange_WhenPdfIsUpdated()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler();
        var hasher = new Sha256Hasher();
        var pdfReader = new PdfPigLayoutPdfReader();
        var differ = new DiffPlexDiffer();

        // Load test PDFs
        var firstPdfBytes = TestDataLoader.LoadPdfFile("PdfTest_First.pdf");
        var secondPdfBytes = TestDataLoader.LoadPdfFile("PdfTest_Second.pdf");

        // Setup mock HTTP responses
        var pdfUrl = "https://www.uscis.gov/sites/default/files/document/forms/i-751.pdf";

        // First check - serve first version
        mockHandler.AddPdfResponse(pdfUrl, firstPdfBytes);
        var httpClient1 = new HttpClient(mockHandler);

        // Act - First check
        using var response1 = await httpClient1.GetAsync(pdfUrl);
        using var stream1 = await response1.Content.ReadAsStreamAsync();
        var text1 = pdfReader.GetPdfText(stream1);
        var hash1 = hasher.ComputeHash(text1);

        // Simulate time passing and PDF being updated
        mockHandler.AddPdfResponse(pdfUrl, secondPdfBytes); // Override with second version
        var httpClient2 = new HttpClient(mockHandler);

        // Act - Second check (after PDF was updated)
        using var response2 = await httpClient2.GetAsync(pdfUrl);
        using var stream2 = await response2.Content.ReadAsStreamAsync();
        var text2 = pdfReader.GetPdfText(stream2);
        var hash2 = hasher.ComputeHash(text2);

        // Assert - Hashes should be different
        Assert.NotEqual(hash1, hash2);

        // Generate diff
        var diffLines = differ.GetDiffLines(text1, text2);
        var totalChanges = diffLines.AddedLines.Count + diffLines.DeletedLines.Count + diffLines.ModifiedLines.Count;

        Assert.True(totalChanges > 0, "Should detect changes between versions");

        // Create a change record like the real system would
        var change = new PdfFormChange
        {
            FileName = "i-751.pdf",
            FullLink = pdfUrl,
            FormName = "I-751",
            OldHash = hash1,
            NewHash = hash2,
            DiffLinesSerialized = System.Text.Json.JsonSerializer.Serialize(diffLines),
            DetectedChangeTime = DateTime.UtcNow
        };

        Assert.NotNull(change);
        Assert.NotEqual(change.OldHash, change.NewHash);
    }
}
