using USCISFormTracker.Core.Models;

namespace USCISFormTracker.Core;

public interface IDiffer
{
    DiffLines GetDiffLines(string oldText, string newText);
}
