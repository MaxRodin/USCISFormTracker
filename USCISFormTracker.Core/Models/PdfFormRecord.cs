namespace USCISFormTracker.Core.Models;

public class PdfFormRecord
{
    public int Id { get; set; }
    public required string Link { get; set; }
    public required string FormName { get; set; }
    public required string Hash { get; set; }
    public DateTime LastChecked { get; set; }
}
