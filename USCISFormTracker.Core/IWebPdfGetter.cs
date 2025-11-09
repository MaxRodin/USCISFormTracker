namespace USCISFormTracker.Core;

public interface IWebPdfGetter
{
  IEnumerable<string> GetPdfLinks();
}
