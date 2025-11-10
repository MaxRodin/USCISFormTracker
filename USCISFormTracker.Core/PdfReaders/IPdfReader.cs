namespace USCISFormTracker.Core.PdfReaders;

public interface IPdfReader
{
  string GetPdfText(Stream stream);
}
