using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using USCISFormTracker.Core.Models;

namespace USCISFormTracker.Core;

public class UscisWebPdfGetter : IWebPdfGetter
{
    private readonly HttpClient _httpClient;
    private readonly string _formsPageUrl;
    private readonly ILogger<UscisWebPdfGetter> _logger;

    public UscisWebPdfGetter(HttpClient httpClient, string formsPageUrl, ILogger<UscisWebPdfGetter> logger)
    {
        _httpClient = httpClient;
        _formsPageUrl = formsPageUrl;
        _logger = logger;
    }

    public async Task<IEnumerable<ScrapedPdf>> GetPdfLinksAsync()
    {
        // Step 1: Get the all-forms page and extract form detail links
        var formDetailLinks = await GetFormDetailLinksAsync();

        // Step 2: For each form detail page, fetch it and extract the PDF link
        var pdfLinks = new List<ScrapedPdf>();
        foreach (var detailLink in formDetailLinks)
        {
            var pdfLinkInfo = await GetPdfLinkFromDetailPageAsync(detailLink);
            if (pdfLinkInfo != null)
            {
                pdfLinks.Add(pdfLinkInfo);
            }
        }

        return pdfLinks;
    }

    private async Task<IEnumerable<string>> GetFormDetailLinksAsync()
    {
        var html = await _httpClient.GetStringAsync(_formsPageUrl);
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

    private async Task<ScrapedPdf?> GetPdfLinkFromDetailPageAsync(string detailPageUrl)
    {
        try
        {
            var html = await _httpClient.GetStringAsync(detailPageUrl);
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

                return new ScrapedPdf
                {
                    FileName = fileName,
                    FullLink = fullLink
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch detail page {DetailPageUrl}, skipping", detailPageUrl);
        }

        return null;
    }
}
