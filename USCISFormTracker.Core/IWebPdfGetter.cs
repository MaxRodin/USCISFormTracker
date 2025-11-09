using USCISFormTracker.Core.Models;

namespace USCISFormTracker.Core;

public interface IWebPdfGetter
{
  IEnumerable<PdfLinkInfo> GetPdfLinks();
}
