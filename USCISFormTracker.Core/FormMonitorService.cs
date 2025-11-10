using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.Logging;
using USCISFormTracker.Core.Data;
using USCISFormTracker.Core.Models;
using USCISFormTracker.Core.PdfReaders;
using USCISFormTracker.Dto;

namespace USCISFormTracker.Core;

public class FormMonitorService
{
    private readonly IWebPdfGetter _webPdfGetter;
    private readonly IPdfReader _pdfReader;
    private readonly IHasher _hasher;
    private readonly IDiffer _differ;
    private readonly IFormRepository _repository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly HttpClient _httpClient;
    private readonly ILogger<FormMonitorService> _logger;

    public FormMonitorService(
        IWebPdfGetter webPdfGetter,
        IPdfReader pdfReader,
        IHasher hasher,
        IDiffer differ,
        IFormRepository repository,
        IPublishEndpoint publishEndpoint,
        HttpClient httpClient,
        ILogger<FormMonitorService> logger)
    {
        _webPdfGetter = webPdfGetter;
        _pdfReader = pdfReader;
        _hasher = hasher;
        _differ = differ;
        _repository = repository;
        _publishEndpoint = publishEndpoint;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task MonitorFormsAsync()
    {
        _logger.LogInformation("Starting USCIS form monitoring...");

        var pdfLinks = _webPdfGetter.GetPdfLinks();
        _logger.LogInformation("Found {Count} PDF links", pdfLinks.Count());

        foreach (var pdfLinkInfo in pdfLinks)
        {
            try
            {
                await ProcessFormAsync(pdfLinkInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing form: {FileName}", pdfLinkInfo.FileName);
            }
        }

        _logger.LogInformation("USCIS form monitoring completed");
    }

    private async Task ProcessFormAsync(PdfLinkInfo pdfLinkInfo)
    {
        _logger.LogInformation("Processing form: {FileName}", pdfLinkInfo.FileName);

        // Download PDF
        using var response = await _httpClient.GetAsync(pdfLinkInfo.FullLink);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to download PDF from {Link}: {StatusCode}", pdfLinkInfo.FullLink, response.StatusCode);
            return;
        }

        using var stream = await response.Content.ReadAsStreamAsync();

        // Extract text
        var text = _pdfReader.GetPdfText(stream);

        // Compute hash
        var hash = _hasher.ComputeHash(text);

        // Extract form name from filename
        var formName = ExtractFormName(pdfLinkInfo.FileName);

        // Check if we've seen this form before
        var existingRecord = await _repository.GetFormRecordByLinkAsync(pdfLinkInfo.FileName);

        if (existingRecord == null)
        {
            // First time seeing this form - just store it
            _logger.LogInformation("New form discovered: {FormName} ({FileName})", formName, pdfLinkInfo.FileName);
            var newRecord = new PdfFormRecord
            {
                FileName = pdfLinkInfo.FileName,
                FullLink = pdfLinkInfo.FullLink,
                FormName = formName,
                Hash = hash,
                LastChecked = DateTime.UtcNow
            };
            await _repository.AddFormRecordAsync(newRecord);

            // Publish FormAddedMessage
            await _publishEndpoint.Publish(new FormAddedMessage
            {
                FileName = pdfLinkInfo.FileName,
                FullLink = pdfLinkInfo.FullLink,
                FormName = formName,
                Hash = hash,
                DiscoveredTime = DateTime.UtcNow
            });
        }
        else if (existingRecord.Hash != hash)
        {
            // Hash changed - form was updated!
            _logger.LogWarning("Form changed detected: {FormName} ({FileName})", formName, pdfLinkInfo.FileName);

            // Re-download and extract text from old version (if we stored it)
            // For now, we'll just use the diff with empty old text
            // In a production system, you might want to store the actual PDF or text content

            var diffLines = _differ.GetDiffLines("", text); // Simplified for now

            var change = new PdfFormChange
            {
                FileName = pdfLinkInfo.FileName,
                FullLink = pdfLinkInfo.FullLink,
                FormName = formName,
                OldHash = existingRecord.Hash,
                NewHash = hash,
                DiffLinesSerialized = JsonSerializer.Serialize(diffLines),
                DetectedChangeTime = DateTime.UtcNow
            };

            // Save change to database
            await _repository.AddFormChangeAsync(change);

            // Publish FormChangeDetectedMessage
            await _publishEndpoint.Publish(new FormChangeDetectedMessage
            {
                FileName = pdfLinkInfo.FileName,
                FullLink = pdfLinkInfo.FullLink,
                FormName = formName,
                OldHash = existingRecord.Hash,
                NewHash = hash,
                DetectedChangeTime = DateTime.UtcNow,
                AddedLines = diffLines.AddedLines,
                DeletedLines = diffLines.DeletedLines,
                ModifiedLines = diffLines.ModifiedLines
            });

            // Update the stored record
            existingRecord.Hash = hash;
            existingRecord.LastChecked = DateTime.UtcNow;
            await _repository.UpdateFormRecordAsync(existingRecord);

            _logger.LogInformation("Change notification message published for {FormName}", formName);
        }
        else
        {
            // No change
            _logger.LogInformation("No change detected for {FormName}", formName);
            existingRecord.LastChecked = DateTime.UtcNow;
            await _repository.UpdateFormRecordAsync(existingRecord);
        }
    }

    private string ExtractFormName(string fileName)
    {
        // Extract form name from filename
        // e.g., i-485.pdf -> i-485
        var formName = Path.GetFileNameWithoutExtension(fileName);
        return formName;
    }
}
