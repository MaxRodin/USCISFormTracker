using HtmlAgilityPack;
using USCISFormTracker.Core.Models;

namespace USCISFormTracker.Core;

public class UscisWebPdfGetter : IWebPdfGetter
{
    private readonly HttpClient _httpClient;
    private readonly string _formsPageUrl;

    public UscisWebPdfGetter(HttpClient httpClient, string formsPageUrl)
    {
        _httpClient = httpClient;
        _formsPageUrl = formsPageUrl;
    }

    public IEnumerable<PdfLinkInfo> GetPdfLinks()
    {
        // Step 1: Get the all-forms page and extract form detail links
        var formDetailLinks = GetFormDetailLinks();

        // Step 2: For each form detail page, fetch it and extract the PDF link
        var pdfLinks = new List<PdfLinkInfo>();
        foreach (var detailLink in formDetailLinks)
        {
            var pdfLinkInfo = GetPdfLinkFromDetailPage(detailLink);
            if (pdfLinkInfo != null)
            {
                pdfLinks.Add(pdfLinkInfo);
            }
        }

        return pdfLinks;
    }

    private IEnumerable<string> GetFormDetailLinks()
    {
        var html = _httpClient.GetStringAsync(_formsPageUrl).Result;
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Find all form detail links (e.g., /i-694, /ar-11, etc.)
        // These are links with class="link link--form-title"
        var detailLinks = doc.DocumentNode
            .SelectNodes("//a[@class='link link--form-title']")
            ?.Select(node => node.GetAttributeValue("href", ""))
            .Where(href => !string.IsNullOrWhiteSpace(href))
            .Select(href => href.StartsWith("http") ? href : $"https://www.uscis.gov{href}")
            .Distinct()
            .ToList() ?? new List<string>();

        return detailLinks;
    }

    private PdfLinkInfo? GetPdfLinkFromDetailPage(string detailPageUrl)
    {
        try
        {
            var html = _httpClient.GetStringAsync(detailPageUrl).Result;
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Find the form PDF link (not instruction PDF) on the detail page
            // We want links where the text starts with "Form " (e.g., "Form I-694")
            var formPdfNode = doc.DocumentNode
                .SelectNodes("//a[@href and @type='application/pdf']")
                ?.FirstOrDefault(node =>
                {
                    var href = node.GetAttributeValue("href", "");
                    var text = node.InnerText;
                    return href.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                        && text.Trim().StartsWith("Form ", StringComparison.OrdinalIgnoreCase)
                        && !text.Contains("Instructions", StringComparison.OrdinalIgnoreCase);
                });

            if (formPdfNode != null)
            {
                var href = formPdfNode.GetAttributeValue("href", "");
                var fullLink = href.StartsWith("http") ? href : $"https://www.uscis.gov{href}";

                // Extract filename from the href (e.g., "i-751.pdf" from "/sites/default/files/document/forms/i-751.pdf")
                var fileName = href.Split('/').Last();

                return new PdfLinkInfo
                {
                    FileName = fileName,
                    FullLink = fullLink
                };
            }
        }
        catch
        {
            // If we can't fetch a detail page, just skip it
        }

        return null;
    }
}
