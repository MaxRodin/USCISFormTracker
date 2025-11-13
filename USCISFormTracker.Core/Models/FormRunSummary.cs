namespace USCISFormTracker.Core.Models;

/// <summary>
/// Summary of a complete monitoring run - returned by Core after comparison
/// </summary>
public class FormRunSummary
{
    public List<AddedForm> AddedForms { get; set; } = new();
    public List<ChangedForm> ChangedForms { get; set; } = new();
    public List<DeletedForm> DeletedForms { get; set; } = new();
    public DateTime RunTime { get; set; }
    public int TotalFormsOnWebsite { get; set; }
    public int TotalProcessed { get; set; }
}

/// <summary>
/// Represents a newly discovered form
/// </summary>
public class AddedForm
{
    public required string FileName { get; set; }
    public required string FullLink { get; set; }
    public required string FormName { get; set; }
    public required string Hash { get; set; }
    public required string ExtractedText { get; set; }
}

/// <summary>
/// Represents a form that has changed since last check
/// </summary>
public class ChangedForm
{
    public required string FileName { get; set; }
    public required string FullLink { get; set; }
    public required string FormName { get; set; }
    public required string OldHash { get; set; }
    public required string NewHash { get; set; }
    public required string OldText { get; set; }
    public required string NewText { get; set; }
    public required DiffLines Diff { get; set; }
}

/// <summary>
/// Represents a form that is no longer on the USCIS website
/// </summary>
public class DeletedForm
{
    public required string FileName { get; set; }
    public required string FormName { get; set; }
    public required string LastKnownLink { get; set; }
}
