using USCISFormTracker.Core.Models;

namespace USCISFormTracker.Core;

public class TextDiffer : IDiffer
{
    public DiffLines GetDiffLines(string oldText, string newText)
    {
        var oldLines = oldText.Split('\n', StringSplitOptions.None);
        var newLines = newText.Split('\n', StringSplitOptions.None);

        var oldSet = new HashSet<string>(oldLines);
        var newSet = new HashSet<string>(newLines);

        var diffLines = new DiffLines
        {
            DeletedLines = oldSet.Except(newSet).ToList(),
            AddedLines = newSet.Except(oldSet).ToList()
        };

        // For modified lines, we'll use line-by-line comparison
        var modifiedLines = new List<string>();
        int maxLines = Math.Max(oldLines.Length, newLines.Length);

        for (int i = 0; i < maxLines; i++)
        {
            var oldLine = i < oldLines.Length ? oldLines[i] : "";
            var newLine = i < newLines.Length ? newLines[i] : "";

            if (oldLine != newLine && !string.IsNullOrWhiteSpace(oldLine) && !string.IsNullOrWhiteSpace(newLine))
            {
                modifiedLines.Add($"Line {i + 1}: '{oldLine}' → '{newLine}'");
            }
        }

        diffLines.ModifiedLines = modifiedLines;

        return diffLines;
    }
}
