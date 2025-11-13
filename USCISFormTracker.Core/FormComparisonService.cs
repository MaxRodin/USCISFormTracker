using Microsoft.Extensions.Logging;
using USCISFormTracker.Core.Models;
using USCISFormTracker.Core.PdfReaders;

namespace USCISFormTracker.Core;

/// <summary>
/// Pure form comparison logic - no database, no message bus
/// </summary>
public class FormComparisonService : IFormComparisonService
{
    private readonly IWebPdfGetter _webPdfGetter;
    private readonly IPdfReader _pdfReader;
    private readonly IHasher _hasher;
    private readonly IDiffer _differ;
    private readonly ILogger<FormComparisonService> _logger;

    public FormComparisonService(
        IWebPdfGetter webPdfGetter,
        IPdfReader pdfReader,
        IHasher hasher,
        IDiffer differ,
        ILogger<FormComparisonService> logger)
    {
        _webPdfGetter = webPdfGetter;
        _pdfReader = pdfReader;
        _hasher = hasher;
        _differ = differ;
        _logger = logger;
    }

    public async Task<FormRunSummary> CompareFormsAsync(
        IEnumerable<FormSnapshot> existingSnapshots,
        HttpClient httpClient)
    {
        var runTime = DateTime.UtcNow;
        var summary = new FormRunSummary { RunTime = runTime };

        _logger.LogInformation("Starting form comparison at {RunTime}", runTime);

        // Get current PDFs from USCIS website
        var pdfLinks = (await _webPdfGetter.GetPdfLinksAsync()).ToList();
        summary.TotalFormsOnWebsite = pdfLinks.Count;
        _logger.LogInformation("Found {Count} PDF links on USCIS website", pdfLinks.Count);

        // Create lookup for existing forms
        var existingDict = existingSnapshots.ToDictionary(s => s.FileName, s => s);
        var processedFileNames = new HashSet<string>();

        // Process each form from website
        foreach (var pdfLinkInfo in pdfLinks)
        {
            try
            {
                processedFileNames.Add(pdfLinkInfo.FileName);
                await ProcessFormAsync(pdfLinkInfo, existingDict, summary, httpClient);
                summary.TotalProcessed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing form: {FileName}", pdfLinkInfo.FileName);
            }
        }

        // Find deleted forms (in database but not on website)
        var deletedFileNames = existingDict.Keys.Except(processedFileNames);
        foreach (var fileName in deletedFileNames)
        {
            var existing = existingDict[fileName];
            summary.DeletedForms.Add(new DeletedForm
            {
                FileName = fileName,
                FormName = existing.FormName,
                LastKnownLink = existing.FullLink
            });
            _logger.LogWarning("Form deleted from website: {FormName} ({FileName})", existing.FormName, fileName);
        }

        _logger.LogInformation(
            "Comparison complete: {Added} added, {Changed} changed, {Deleted} deleted",
            summary.AddedForms.Count,
            summary.ChangedForms.Count,
            summary.DeletedForms.Count);

        return summary;
    }

    private async Task ProcessFormAsync(
        ScrapedPdf scrapedPdf,
        Dictionary<string, FormSnapshot> existingDict,
        FormRunSummary summary,
        HttpClient httpClient)
    {
        _logger.LogDebug("Processing form: {FileName}", scrapedPdf.FileName);

        // Download PDF
        using var response = await httpClient.GetAsync(scrapedPdf.FullLink);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Failed to download PDF from {Link}: {StatusCode}",
                scrapedPdf.FullLink,
                response.StatusCode);
            return;
        }

        using var stream = await response.Content.ReadAsStreamAsync();

        // Extract text
        var text = _pdfReader.GetPdfText(stream);

        // Compute hash
        var hash = _hasher.ComputeHash(text);

        // Extract form name from filename
        var formName = ExtractFormName(scrapedPdf.FileName);

        // Check if we've seen this form before
        if (!existingDict.TryGetValue(scrapedPdf.FileName, out var existingSnapshot))
        {
            // New form discovered
            _logger.LogInformation("New form discovered: {FormName} ({FileName})", formName, scrapedPdf.FileName);
            summary.AddedForms.Add(new AddedForm
            {
                FileName = scrapedPdf.FileName,
                FullLink = scrapedPdf.FullLink,
                FormName = formName,
                Hash = hash,
                ExtractedText = text
            });
        }
        else if (existingSnapshot.Hash != hash)
        {
            // Form changed
            _logger.LogWarning("Form change detected: {FormName} ({FileName})", formName, scrapedPdf.FileName);

            // Generate diff using stored old text
            var diffLines = _differ.GetDiffLines(existingSnapshot.ExtractedText, text);

            summary.ChangedForms.Add(new ChangedForm
            {
                FileName = scrapedPdf.FileName,
                FullLink = scrapedPdf.FullLink,
                FormName = formName,
                OldHash = existingSnapshot.Hash,
                NewHash = hash,
                OldText = existingSnapshot.ExtractedText,
                NewText = text,
                Diff = diffLines
            });
        }
        else
        {
            // No change
            _logger.LogDebug("No change detected for {FormName}", formName);
        }
    }

    private string ExtractFormName(string fileName)
    {
        // Extract form name from filename (e.g., i-485.pdf -> i-485)
        return Path.GetFileNameWithoutExtension(fileName);
    }
}
