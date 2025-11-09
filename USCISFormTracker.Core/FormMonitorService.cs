using System.Text.Json;
using Microsoft.Extensions.Logging;
using USCISFormTracker.Core.Data;
using USCISFormTracker.Core.Models;

namespace USCISFormTracker.Core;

public class FormMonitorService
{
    private readonly IWebPdfGetter _webPdfGetter;
    private readonly IPdfReader _pdfReader;
    private readonly IHasher _hasher;
    private readonly IDiffer _differ;
    private readonly IFormRepository _repository;
    private readonly IEmailService _emailService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<FormMonitorService> _logger;

    public FormMonitorService(
        IWebPdfGetter webPdfGetter,
        IPdfReader pdfReader,
        IHasher hasher,
        IDiffer differ,
        IFormRepository repository,
        IEmailService emailService,
        HttpClient httpClient,
        ILogger<FormMonitorService> logger)
    {
        _webPdfGetter = webPdfGetter;
        _pdfReader = pdfReader;
        _hasher = hasher;
        _differ = differ;
        _repository = repository;
        _emailService = emailService;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task MonitorFormsAsync()
    {
        _logger.LogInformation("Starting USCIS form monitoring...");

        var pdfLinks = _webPdfGetter.GetPdfLinks();
        _logger.LogInformation("Found {Count} PDF links", pdfLinks.Count());

        foreach (var link in pdfLinks)
        {
            try
            {
                await ProcessFormAsync(link);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing form: {Link}", link);
            }
        }

        _logger.LogInformation("USCIS form monitoring completed");
    }

    private async Task ProcessFormAsync(string link)
    {
        _logger.LogInformation("Processing form: {Link}", link);

        // Download PDF
        using var response = await _httpClient.GetAsync(link);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to download PDF from {Link}: {StatusCode}", link, response.StatusCode);
            return;
        }

        using var stream = await response.Content.ReadAsStreamAsync();

        // Extract text
        var text = _pdfReader.GetPdfText(stream);

        // Compute hash
        var hash = _hasher.ComputeHash(text);

        // Extract form name from link
        var formName = ExtractFormName(link);

        // Check if we've seen this form before
        var existingRecord = await _repository.GetFormRecordByLinkAsync(link);

        if (existingRecord == null)
        {
            // First time seeing this form - just store it
            _logger.LogInformation("New form discovered: {FormName} ({Link})", formName, link);
            await _repository.AddFormRecordAsync(new PdfFormRecord
            {
                Link = link,
                FormName = formName,
                Hash = hash,
                LastChecked = DateTime.UtcNow
            });
        }
        else if (existingRecord.Hash != hash)
        {
            // Hash changed - form was updated!
            _logger.LogWarning("Form changed detected: {FormName} ({Link})", formName, link);

            // Re-download and extract text from old version (if we stored it)
            // For now, we'll just use the diff with empty old text
            // In a production system, you might want to store the actual PDF or text content

            var diffLines = _differ.GetDiffLines("", text); // Simplified for now

            var change = new PdfFormChange
            {
                Link = link,
                FormName = formName,
                OldHash = existingRecord.Hash,
                NewHash = hash,
                DiffLinesSerialized = JsonSerializer.Serialize(diffLines),
                DetectedChangeTime = DateTime.UtcNow
            };

            // Save change to database
            await _repository.AddFormChangeAsync(change);

            // Send email notification
            await _emailService.SendChangeNotificationAsync(change, diffLines);

            // Update the stored record
            existingRecord.Hash = hash;
            existingRecord.LastChecked = DateTime.UtcNow;
            await _repository.UpdateFormRecordAsync(existingRecord);

            _logger.LogInformation("Change notification sent for {FormName}", formName);
        }
        else
        {
            // No change
            _logger.LogInformation("No change detected for {FormName}", formName);
            existingRecord.LastChecked = DateTime.UtcNow;
            await _repository.UpdateFormRecordAsync(existingRecord);
        }
    }

    private string ExtractFormName(string link)
    {
        // Extract form name from URL
        // e.g., https://www.uscis.gov/sites/default/files/document/forms/i-485.pdf -> i-485
        var parts = link.Split('/');
        var fileName = parts[^1]; // Last part
        var formName = Path.GetFileNameWithoutExtension(fileName);
        return formName;
    }
}
