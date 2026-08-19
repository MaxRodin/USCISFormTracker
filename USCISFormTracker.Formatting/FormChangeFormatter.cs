using System.Text;
using System.Text.Json;
using USCISFormTracker.Core.Models;

namespace USCISFormTracker.Formatting;

public class FormChangeFormatter : IFormChangeFormatter
{
    public string FormatAsHtml(PdfFormChange change)
    {
        var diffLines = DeserializeDiffLines(change.DiffLinesSerialized);
        var sb = new StringBuilder();

        sb.AppendLine("<html><body>");
        sb.AppendLine("<h2>USCIS Form Change Details</h2>");

        // Basic info
        sb.AppendLine("<div style=\"margin-bottom: 20px;\">");
        sb.AppendLine($"<p><strong>Form:</strong> {HtmlEncode(change.FormName)}</p>");
        sb.AppendLine($"<p><strong>Link:</strong> <a href=\"{HtmlEncode(change.FullLink)}\" target=\"_blank\">{HtmlEncode(change.FullLink)}</a></p>");
        sb.AppendLine($"<p><strong>Detected:</strong> {change.DetectedChangeTime:yyyy-MM-dd HH:mm:ss} UTC</p>");
        sb.AppendLine("</div>");

        sb.AppendLine("<hr>");

        // Diff section
        var totalLines = (diffLines.AddedLines?.Count ?? 0) +
                        (diffLines.DeletedLines?.Count ?? 0) +
                        (diffLines.ModifiedLines?.Count ?? 0);

        if (totalLines > 0)
        {
            sb.AppendLine("<h3>Changes</h3>");
            sb.AppendLine("<div style=\"font-family: 'SF Mono', 'Monaco', 'Inconsolata', 'Consolas', 'Courier New', monospace; font-size: 13px; background: #f5f5f5; padding: 15px; border-radius: 5px; line-height: 1.6;\">");

            if (diffLines.AddedLines?.Count > 0)
            {
                sb.AppendLine("<div style=\"color: green; margin-bottom: 10px;\"><strong>Added Lines:</strong></div>");
                foreach (var line in diffLines.AddedLines)
                {
                    sb.AppendLine($"<div style=\"color: green; margin-left: 10px;\">+ {HtmlEncode(line)}</div>");
                }
                sb.AppendLine("<br>");
            }

            if (diffLines.DeletedLines?.Count > 0)
            {
                sb.AppendLine("<div style=\"color: red; margin-bottom: 10px;\"><strong>Deleted Lines:</strong></div>");
                foreach (var line in diffLines.DeletedLines)
                {
                    sb.AppendLine($"<div style=\"color: red; margin-left: 10px;\">- {HtmlEncode(line)}</div>");
                }
                sb.AppendLine("<br>");
            }

            if (diffLines.ModifiedLines?.Count > 0)
            {
                sb.AppendLine("<div style=\"color: orange; margin-bottom: 10px;\"><strong>Modified Lines:</strong></div>");
                foreach (var line in diffLines.ModifiedLines)
                {
                    sb.AppendLine($"<div style=\"color: orange; margin-left: 10px;\">~ {HtmlEncode(line)}</div>");
                }
            }

            sb.AppendLine("</div>");
        }
        else
        {
            sb.AppendLine("<p style=\"font-style: italic; color: #666;\">No diff details available</p>");
        }

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    public string FormatAsText(PdfFormChange change)
    {
        var diffLines = DeserializeDiffLines(change.DiffLinesSerialized);
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("USCIS Form Change Details");
        sb.AppendLine("=========================");
        sb.AppendLine();

        // Basic info
        sb.AppendLine($"Form: {change.FormName}");
        sb.AppendLine($"Link: {change.FullLink}");
        sb.AppendLine($"Detected: {change.DetectedChangeTime:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();

        // Diff section
        var totalLines = (diffLines.AddedLines?.Count ?? 0) +
                        (diffLines.DeletedLines?.Count ?? 0) +
                        (diffLines.ModifiedLines?.Count ?? 0);

        if (totalLines > 0)
        {
            sb.AppendLine("Changes:");
            sb.AppendLine("--------");
            sb.AppendLine();

            if (diffLines.AddedLines?.Count > 0)
            {
                sb.AppendLine("Added Lines:");
                foreach (var line in diffLines.AddedLines)
                {
                    sb.AppendLine($"  + {line}");
                }
                sb.AppendLine();
            }

            if (diffLines.DeletedLines?.Count > 0)
            {
                sb.AppendLine("Deleted Lines:");
                foreach (var line in diffLines.DeletedLines)
                {
                    sb.AppendLine($"  - {line}");
                }
                sb.AppendLine();
            }

            if (diffLines.ModifiedLines?.Count > 0)
            {
                sb.AppendLine("Modified Lines:");
                foreach (var line in diffLines.ModifiedLines)
                {
                    sb.AppendLine($"  ~ {line}");
                }
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("No diff details available");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static DiffLines DeserializeDiffLines(string serialized)
    {
        try
        {
            return JsonSerializer.Deserialize<DiffLines>(serialized) ?? new DiffLines();
        }
        catch
        {
            // If deserialization fails, return empty DiffLines
            return new DiffLines();
        }
    }

    private static string HtmlEncode(string text)
    {
        return System.Net.WebUtility.HtmlEncode(text);
    }
}
