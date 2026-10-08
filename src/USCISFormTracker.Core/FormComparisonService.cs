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
    private readonly IPdfFileManager _pdfFileManager;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FormComparisonService> _logger;

    public FormComparisonService(
        IWebPdfGetter webPdfGetter,
        IPdfReader pdfReader,
        IHasher hasher,
        IDiffer differ,
        IPdfFileManager pdfFileManager,
        IHttpClientFactory httpClientFactory,
        ILogger<FormComparisonService> logger)
    {
        _webPdfGetter = webPdfGetter;
        _pdfReader = pdfReader;
        _hasher = hasher;
        _differ = differ;
        _pdfFileManager = pdfFileManager;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<FormRunSummary> CompareFormsAsync(IEnumerable<PdfFormRecord> existingRecords)
    {
        var httpClient = _httpClientFactory.CreateClient();
        var runTime = DateTime.UtcNow;
        var summary = new FormRunSummary { RunTime = runTime };

        _logger.LogInformation("Starting form comparison at {RunTime}", runTime);

        // Get current PDFs from USCIS website
        var pdfLinks = (await _webPdfGetter.GetPdfLinksAsync()).ToList();
        summary.TotalFormsOnWebsite = pdfLinks.Count;
        _logger.LogInformation("Found {Count} PDF links on USCIS website", pdfLinks.Count);

        // Create lookup for existing forms
        var existingDict = existingRecords.ToDictionary(r => r.FileName, r => r);
        var processedFileNames = new HashSet<string>();

        // Process each form from website
        foreach (var pdfLinkInfo in pdfLinks)
        {
            try
            {
                processedFileNames.Add(pdfLinkInfo.FileName);
                await ProcessFormAsync(pdfLinkInfo, existingDict, summary, httpClient);
                summary.TotalProcessed++;

                // Rate limiting: 100ms delay between requests (max 10 requests/sec)
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing form: {FileName}", pdfLinkInfo.FileName);
            }
        }

        // Find deleted forms (in database but not on website) using helper
        var deletedForms = FormComparisonHelper.GetDeletedForms(existingRecords, pdfLinks);
        foreach (var deletedForm in deletedForms)
        {
            summary.DeletedForms.Add(deletedForm);
            _logger.LogWarning("Form deleted from website: {FormName} ({FileName})", deletedForm.FormName, deletedForm.FileName);
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
        Dictionary<string, PdfFormRecord> existingDict,
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

        // Read PDF into byte array so we can reuse it for both text extraction and file saving
        var pdfBytes = await response.Content.ReadAsByteArrayAsync();

        // Extract text from byte array
        using var stream = new MemoryStream(pdfBytes);
        var text = _pdfReader.GetPdfText(stream);

        // Compute hash
        var hash = _hasher.ComputeHash(text);

        // Extract form name from filename
        var formName = ExtractFormName(scrapedPdf.FileName);

        // Check if we've seen this form before
        if (!existingDict.TryGetValue(scrapedPdf.FileName, out var existingRecord))
        {
            // New form discovered
            _logger.LogInformation("New form discovered: {FormName} ({FileName})", formName, scrapedPdf.FileName);

            // Save PDF to disk
            string? pdfPath = null;
            try
            {
                pdfPath = await _pdfFileManager.SavePdfAsync(formName, pdfBytes, summary.RunTime);
                await _pdfFileManager.CleanupOldVersionsAsync(formName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save PDF for new form {FormName}", formName);
                // Continue without PDF path - don't fail the entire comparison
            }

            summary.AddedForms.Add(new AddedForm
            {
                FileName = scrapedPdf.FileName,
                FullLink = scrapedPdf.FullLink,
                FormName = formName,
                Hash = hash,
                ExtractedText = text,
                PdfPath = pdfPath
            });
        }
        else if (existingRecord.Hash != hash)
        {
            // Form changed
            _logger.LogWarning("Form change detected: {FormName} ({FileName})", formName, scrapedPdf.FileName);

            // Save new PDF version to disk
            string? newPdfPath = null;
            try
            {
                newPdfPath = await _pdfFileManager.SavePdfAsync(formName, pdfBytes, summary.RunTime);
                await _pdfFileManager.CleanupOldVersionsAsync(formName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save PDF for changed form {FormName}", formName);
                // Continue without PDF path - don't fail the entire comparison
            }

            // Generate diff using stored old text
            var diffLines = _differ.GetDiffLines(existingRecord.ExtractedText, text);

            summary.ChangedForms.Add(new ChangedForm
            {
                FileName = scrapedPdf.FileName,
                FullLink = scrapedPdf.FullLink,
                FormName = formName,
                OldHash = existingRecord.Hash,
                NewHash = hash,
                OldText = existingRecord.ExtractedText,
                NewText = text,
                Diff = diffLines,
                OldPdfPath = existingRecord.LatestPdfPath,
                NewPdfPath = newPdfPath
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
