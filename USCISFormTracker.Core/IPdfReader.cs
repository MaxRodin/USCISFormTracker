namespace USCISFormTracker.Core;

public interface IPdfReader
{
  string GetPdfText(Stream stream);
}
