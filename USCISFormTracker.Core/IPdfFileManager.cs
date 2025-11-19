namespace USCISFormTracker.Core;

/// <summary>
/// Manages PDF file storage with timestamped filenames.
/// </summary>
public interface IPdfFileManager
{
    /// <summary>
    /// Saves PDF stream to file system with timestamped filename.
    /// </summary>
    /// <param name="formName">Form identifier (e.g., "i-751" from filename "i-751.pdf")</param>
    /// <param name="pdfBytes">PDF content as byte array</param>
    /// <param name="timestamp">Timestamp for filename</param>
    /// <returns>Relative path to saved file (e.g., "pdfs/i-751/i-751_2025-11-19T12-30-45-123Z.pdf")</returns>
    Task<string> SavePdfAsync(string formName, byte[] pdfBytes, DateTime timestamp);

    /// <summary>
    /// Gets the full physical path for a relative path.
    /// </summary>
    /// <param name="relativePath">Relative path from SavePdfAsync</param>
    /// <returns>Full physical path on disk</returns>
    string GetFullPath(string relativePath);

    /// <summary>
    /// Deletes old PDF files for a form, keeping only the N most recent.
    /// </summary>
    /// <param name="formName">Form identifier</param>
    /// <param name="keepCount">Number of recent versions to keep</param>
    Task CleanupOldVersionsAsync(string formName, int keepCount = 10);
}
