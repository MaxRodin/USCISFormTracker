using USCISFormTracker.Core.Models;

namespace USCISFormTracker.Core;

public interface IWebPdfGetter
{
    Task<IEnumerable<ScrapedPdf>> GetPdfLinksAsync();
}
