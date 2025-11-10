using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using USCISFormTracker.Core.Models;

namespace USCISFormTracker.Core;

/// <summary>
/// Diff implementation using DiffPlex library for more sophisticated diff algorithms
/// </summary>
public class DiffPlexDiffer : IDiffer
{
    public DiffLines GetDiffLines(string oldText, string newText)
    {
        // Use DiffPlex's InlineDiffBuilder for line-by-line comparison
        var diffBuilder = new InlineDiffBuilder(new Differ());
        var diff = diffBuilder.BuildDiffModel(oldText, newText);

        var diffLines = new DiffLines
        {
            AddedLines = new List<string>(),
            DeletedLines = new List<string>(),
            ModifiedLines = new List<string>()
        };

        foreach (var line in diff.Lines)
        {
            switch (line.Type)
            {
                case ChangeType.Inserted:
                    diffLines.AddedLines.Add(line.Text);
                    break;

                case ChangeType.Deleted:
                    diffLines.DeletedLines.Add(line.Text);
                    break;

                case ChangeType.Modified:
                    diffLines.ModifiedLines.Add(line.Text);
                    break;

                case ChangeType.Imaginary:
                    // Imaginary lines are placeholders in side-by-side diff, skip them
                    break;

                case ChangeType.Unchanged:
                    // We don't track unchanged lines
                    break;
            }
        }

        return diffLines;
    }
}
