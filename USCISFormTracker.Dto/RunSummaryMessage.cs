namespace USCISFormTracker.Dto;

/// <summary>
/// Message published for aggregate summary (typically on first run)
/// </summary>
public class RunSummaryMessage
{
    public required DateTime RunTime { get; set; }
    public required int TotalFormsOnWebsite { get; set; }
    public required int NewFormsCount { get; set; }
    public required int ChangedFormsCount { get; set; }
    public required int DeletedFormsCount { get; set; }
    public required List<FormSummaryItem> NewForms { get; set; }
    public required List<FormSummaryItem> ChangedForms { get; set; }
    public required List<FormSummaryItem> DeletedForms { get; set; }
}

/// <summary>
/// Lightweight form summary for aggregate messages
/// </summary>
public class FormSummaryItem
{
    public required string FileName { get; set; }
    public required string FormName { get; set; }
    public required string FullLink { get; set; }

    // Diff details (for changed forms only)
    public List<string>? AddedLines { get; set; }
    public List<string>? DeletedLines { get; set; }
    public List<string>? ModifiedLines { get; set; }
}
