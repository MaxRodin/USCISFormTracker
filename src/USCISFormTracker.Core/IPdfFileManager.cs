namespace USCISFormTracker.Core;

/// <summary>
/// Manages PDF file storage with timestamped filenames.
/// Files are written under a configured root directory as
/// {root}/{baseDir}/{formName}/{formName}_{timestamp}.pdf. Returned paths are
/// relative to the root so they stay valid if the root is relocated.
/// </summary>
public interface IPdfFileManager
{
    /// <summary>
    /// Saves PDF bytes to the file system with a timestamped filename.
    /// </summary>
    /// <param name="baseDir">Source subdirectory under the root</param>
    /// <param name="formName">Form identifier (e.g., "i-751")</param>
    /// <param name="pdfBytes">PDF content as byte array</param>
    /// <param name="timestamp">Timestamp for filename</param>
    /// <returns>Path relative to the root (e.g., "{baseDir}/i-751/i-751_2025-11-19T12-30-45-123Z.pdf")</returns>
    Task<string> SavePdfAsync(string baseDir, string formName, byte[] pdfBytes, DateTime timestamp);

    /// <summary>
    /// Deletes old PDF files for a form, keeping only the N most recent.
    /// </summary>
    /// <param name="baseDir">Source subdirectory under the root</param>
    /// <param name="formName">Form identifier</param>
    /// <param name="keepCount">Number of recent versions to keep</param>
    Task CleanupOldVersionsAsync(string baseDir, string formName, int keepCount = 10);
}
