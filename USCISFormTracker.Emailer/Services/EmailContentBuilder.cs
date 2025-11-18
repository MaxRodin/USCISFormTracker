using System.Text;
using USCISFormTracker.Dto;

namespace USCISFormTracker.Emailer.Services;

public class EmailContentBuilder : IEmailContentBuilder
{
    public (string subject, string htmlBody, string textBody) BuildRunSummaryEmail(RunSummaryMessage summary)
    {
        var subject = $"USCIS Form Tracker - Daily Summary ({summary.NewFormsCount} new, {summary.ChangedFormsCount} changed, {summary.DeletedFormsCount} deleted)";

        var htmlBody = BuildHtml(summary);
        var textBody = BuildText(summary);

        return (subject, htmlBody, textBody);
    }

    private string BuildHtml(RunSummaryMessage summary)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<html><body>");

        // Header
        sb.AppendLine("<h2>USCIS Form Tracker - Daily Summary</h2>");
        sb.AppendLine($"<p><strong>Run Time:</strong> {summary.RunTime:yyyy-MM-dd HH:mm:ss} UTC</p>");
        sb.AppendLine("<hr>");

        // Summary stats
        sb.AppendLine("<h3>Summary</h3>");
        sb.AppendLine("<ul>");
        sb.AppendLine($"<li><strong>{summary.TotalFormsOnWebsite}</strong> total forms on USCIS website</li>");
        sb.AppendLine($"<li><strong style=\"color: green;\">{summary.NewFormsCount}</strong> new forms discovered</li>");
        sb.AppendLine($"<li><strong style=\"color: orange;\">{summary.ChangedFormsCount}</strong> forms changed</li>");
        sb.AppendLine($"<li><strong style=\"color: red;\">{summary.DeletedFormsCount}</strong> forms removed</li>");
        sb.AppendLine("</ul>");

        // New forms section
        if (summary.NewForms.Count > 0)
        {
            sb.AppendLine("<h3 style=\"color: green;\">New Forms Discovered</h3>");
            var displayCount = Math.Min(summary.NewForms.Count, 50);
            sb.AppendLine("<ul>");
            foreach (var form in summary.NewForms.Take(displayCount))
            {
                sb.AppendLine($"<li><strong>{HtmlEncode(form.FormName)}</strong> - <a href=\"{HtmlEncode(form.FullLink)}\">{HtmlEncode(form.FileName)}</a></li>");
            }
            sb.AppendLine("</ul>");
            if (summary.NewForms.Count > displayCount)
            {
                sb.AppendLine($"<p><em>... and {summary.NewForms.Count - displayCount} more forms</em></p>");
            }
        }

        // Changed forms section
        if (summary.ChangedForms.Count > 0)
        {
            sb.AppendLine("<h3 style=\"color: orange;\">Changed Forms</h3>");

            foreach (var form in summary.ChangedForms.Take(20))
            {
                sb.AppendLine("<div style=\"margin-bottom: 20px; padding: 10px; border: 1px solid #ddd; border-radius: 5px;\">");
                sb.AppendLine($"<h4><strong>{HtmlEncode(form.FormName)}</strong> - <a href=\"{HtmlEncode(form.FullLink)}\">{HtmlEncode(form.FileName)}</a></h4>");

                // Show diff if total changes < 100 lines
                var totalLines = (form.AddedLines?.Count ?? 0) + (form.DeletedLines?.Count ?? 0) + (form.ModifiedLines?.Count ?? 0);
                if (totalLines > 0 && totalLines < 100)
                {
                    sb.AppendLine("<div style=\"font-family: monospace; font-size: 12px; background: #f5f5f5; padding: 10px; margin-top: 10px;\">");

                    if (form.AddedLines?.Count > 0)
                    {
                        sb.AppendLine("<div style=\"color: green; margin-bottom: 5px;\"><strong>Added:</strong></div>");
                        foreach (var line in form.AddedLines)
                        {
                            sb.AppendLine($"<div style=\"color: green;\">+ {HtmlEncode(line)}</div>");
                        }
                    }

                    if (form.DeletedLines?.Count > 0)
                    {
                        sb.AppendLine("<div style=\"color: red; margin-top: 5px; margin-bottom: 5px;\"><strong>Deleted:</strong></div>");
                        foreach (var line in form.DeletedLines)
                        {
                            sb.AppendLine($"<div style=\"color: red;\">- {HtmlEncode(line)}</div>");
                        }
                    }

                    if (form.ModifiedLines?.Count > 0)
                    {
                        sb.AppendLine("<div style=\"color: orange; margin-top: 5px; margin-bottom: 5px;\"><strong>Modified:</strong></div>");
                        foreach (var line in form.ModifiedLines)
                        {
                            sb.AppendLine($"<div style=\"color: orange;\">~ {HtmlEncode(line)}</div>");
                        }
                    }

                    sb.AppendLine("</div>");
                }
                else if (totalLines >= 100)
                {
                    sb.AppendLine($"<p style=\"font-style: italic; color: #666;\">(Large diff: {totalLines} total changes - view form for details)</p>");
                }

                sb.AppendLine("</div>");
            }

            if (summary.ChangedForms.Count > 20)
            {
                sb.AppendLine($"<p><em>... and {summary.ChangedForms.Count - 20} more</em></p>");
            }
        }

        // Deleted forms section
        if (summary.DeletedForms.Count > 0)
        {
            sb.AppendLine("<h3 style=\"color: red;\">Removed Forms</h3>");
            sb.AppendLine("<ul>");
            foreach (var form in summary.DeletedForms)
            {
                sb.AppendLine($"<li><strong>{HtmlEncode(form.FormName)}</strong> ({HtmlEncode(form.FileName)})</li>");
            }
            sb.AppendLine("</ul>");
        }

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private string BuildText(RunSummaryMessage summary)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("USCIS Form Tracker - Daily Summary");
        sb.AppendLine("===================================");
        sb.AppendLine();
        sb.AppendLine($"Run Time: {summary.RunTime:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();

        // Summary stats
        sb.AppendLine("Summary:");
        sb.AppendLine($"  Total forms on website: {summary.TotalFormsOnWebsite}");
        sb.AppendLine($"  New forms: {summary.NewFormsCount}");
        sb.AppendLine($"  Changed forms: {summary.ChangedFormsCount}");
        sb.AppendLine($"  Removed forms: {summary.DeletedFormsCount}");
        sb.AppendLine();

        // New forms
        if (summary.NewForms.Count > 0)
        {
            sb.AppendLine("=== New Forms Discovered ===");
            var displayCount = Math.Min(summary.NewForms.Count, 50);
            foreach (var form in summary.NewForms.Take(displayCount))
            {
                sb.AppendLine($"  • {form.FormName} ({form.FileName})");
                sb.AppendLine($"    {form.FullLink}");
            }
            if (summary.NewForms.Count > displayCount)
            {
                sb.AppendLine($"  ... and {summary.NewForms.Count - displayCount} more forms");
            }
            sb.AppendLine();
        }

        // Changed forms
        if (summary.ChangedForms.Count > 0)
        {
            sb.AppendLine("=== Changed Forms ===");
            foreach (var form in summary.ChangedForms.Take(20))
            {
                sb.AppendLine($"  • {form.FormName} ({form.FileName})");
                sb.AppendLine($"    {form.FullLink}");

                // Show diff if total changes < 100 lines
                var totalLines = (form.AddedLines?.Count ?? 0) + (form.DeletedLines?.Count ?? 0) + (form.ModifiedLines?.Count ?? 0);
                if (totalLines > 0 && totalLines < 100)
                {
                    sb.AppendLine();
                    if (form.AddedLines?.Count > 0)
                    {
                        sb.AppendLine("    Added:");
                        foreach (var line in form.AddedLines)
                        {
                            sb.AppendLine($"      + {line}");
                        }
                    }

                    if (form.DeletedLines?.Count > 0)
                    {
                        sb.AppendLine("    Deleted:");
                        foreach (var line in form.DeletedLines)
                        {
                            sb.AppendLine($"      - {line}");
                        }
                    }

                    if (form.ModifiedLines?.Count > 0)
                    {
                        sb.AppendLine("    Modified:");
                        foreach (var line in form.ModifiedLines)
                        {
                            sb.AppendLine($"      ~ {line}");
                        }
                    }
                }
                else if (totalLines >= 100)
                {
                    sb.AppendLine($"    (Large diff: {totalLines} total changes - view form for details)");
                }

                sb.AppendLine();
            }
            if (summary.ChangedForms.Count > 20)
            {
                sb.AppendLine($"  ... and {summary.ChangedForms.Count - 20} more");
            }
            sb.AppendLine();
        }

        // Deleted forms
        if (summary.DeletedForms.Count > 0)
        {
            sb.AppendLine("=== Removed Forms ===");
            foreach (var form in summary.DeletedForms)
            {
                sb.AppendLine($"  • {form.FormName} ({form.FileName})");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string HtmlEncode(string text)
    {
        return System.Net.WebUtility.HtmlEncode(text);
    }
}
