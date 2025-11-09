using HtmlAgilityPack;

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

    public IEnumerable<string> GetPdfLinks()
    {
        var html = _httpClient.GetStringAsync(_formsPageUrl).Result;
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Find all PDF links on the page
        var pdfLinks = doc.DocumentNode
            .SelectNodes("//a[@href]")
            ?.Where(node => node.GetAttributeValue("href", "").EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            .Select(node => node.GetAttributeValue("href", ""))
            .Where(href => !string.IsNullOrWhiteSpace(href))
            .Select(href => href.StartsWith("http") ? href : $"https://www.uscis.gov{href}")
            .Distinct()
            .ToList() ?? new List<string>();

        return pdfLinks;
    }
}
