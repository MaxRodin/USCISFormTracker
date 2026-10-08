using Microsoft.Extensions.Logging;

namespace USCISFormTracker.Core;

public class PdfFileManager : IPdfFileManager
{
    private readonly string _rootDirectory;
    private readonly ILogger<PdfFileManager> _logger;

    public PdfFileManager(string rootDirectory, ILogger<PdfFileManager> logger)
    {
        _rootDirectory = rootDirectory ?? throw new ArgumentNullException(nameof(rootDirectory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> SavePdfAsync(string baseDir, string formName, byte[] pdfBytes, DateTime timestamp)
    {
        if (string.IsNullOrWhiteSpace(baseDir))
            throw new ArgumentException("Base directory cannot be null or empty", nameof(baseDir));
        if (string.IsNullOrWhiteSpace(formName))
            throw new ArgumentException("Form name cannot be null or empty", nameof(formName));
        if (pdfBytes == null || pdfBytes.Length == 0)
            throw new ArgumentException("PDF bytes cannot be null or empty", nameof(pdfBytes));

        // Sanitize form name for file system (remove .pdf extension if present)
        var sanitizedFormName = Path.GetFileNameWithoutExtension(formName);

        // Format timestamp as ISO 8601 with file-safe characters (milliseconds precision)
        var timestampString = timestamp.ToUniversalTime().ToString("yyyy-MM-ddTHH-mm-ss-fffZ");

        // Build path relative to the root: {baseDir}/{formname}/{formname}_{timestamp}.pdf
        var fileName = $"{sanitizedFormName}_{timestampString}.pdf";
        var relativePath = Path.Combine(baseDir, sanitizedFormName, fileName);
        var fullPath = GetFullPath(relativePath);

        try
        {
            // Ensure directory exists
            var directoryPath = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            // Save PDF to disk
            await File.WriteAllBytesAsync(fullPath, pdfBytes);

            _logger.LogInformation("Saved PDF to {Path} ({Size} bytes)", fullPath, pdfBytes.Length);

            return relativePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save PDF for form {FormName} to {Path}", formName, fullPath);
            throw;
        }
    }

    public Task CleanupOldVersionsAsync(string baseDir, string formName, int keepCount = 10)
    {
        if (string.IsNullOrWhiteSpace(baseDir))
            throw new ArgumentException("Base directory cannot be null or empty", nameof(baseDir));
        if (string.IsNullOrWhiteSpace(formName))
            throw new ArgumentException("Form name cannot be null or empty", nameof(formName));
        if (keepCount < 1)
            throw new ArgumentException("Keep count must be at least 1", nameof(keepCount));

        var sanitizedFormName = Path.GetFileNameWithoutExtension(formName);
        var fullDirectoryPath = GetFullPath(Path.Combine(baseDir, sanitizedFormName));

        try
        {
            if (!Directory.Exists(fullDirectoryPath))
            {
                _logger.LogDebug("Directory {Directory} does not exist, skipping cleanup", fullDirectoryPath);
                return Task.CompletedTask;
            }

            // Get all PDF files in the form directory, ordered by creation time (newest first)
            var pdfFiles = Directory.GetFiles(fullDirectoryPath, "*.pdf")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTimeUtc)
                .ToList();

            if (pdfFiles.Count <= keepCount)
            {
                _logger.LogDebug("Form {FormName} has {Count} PDF versions, no cleanup needed (keeping {KeepCount})",
                    formName, pdfFiles.Count, keepCount);
                return Task.CompletedTask;
            }

            // Delete files beyond the keep count
            var filesToDelete = pdfFiles.Skip(keepCount).ToList();
            foreach (var file in filesToDelete)
            {
                try
                {
                    file.Delete();
                    _logger.LogInformation("Deleted old PDF version: {FileName}", file.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete old PDF file {FilePath}", file.FullName);
                }
            }

            _logger.LogInformation("Cleaned up {DeletedCount} old PDF versions for form {FormName}, keeping {KeptCount}",
                filesToDelete.Count, formName, pdfFiles.Count - filesToDelete.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup old versions for form {FormName}", formName);
            // Don't throw - cleanup failures shouldn't break the main workflow
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Resolves a root-relative path to an absolute path on disk.
    /// </summary>
    private string GetFullPath(string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
            return relativePath;

        return Path.GetFullPath(Path.Combine(_rootDirectory, relativePath));
    }
}
