namespace USCISFormTracker.Core.Models;

public class DiffLines
{
    public List<string> AddedLines { get; set; } = new();
    public List<string> DeletedLines { get; set; } = new();
    public List<string> ModifiedLines { get; set; } = new();
}
