using System.Net;

namespace USCISFormTracker.Tests.TestHelpers;

/// <summary>
/// Mock HTTP message handler for testing HTTP requests without making actual network calls
/// </summary>
public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, (HttpStatusCode statusCode, byte[] content, string contentType)> _responses = new();

    public void AddResponse(string url, HttpStatusCode statusCode, byte[] content, string contentType = "text/html")
    {
        _responses[url] = (statusCode, content, contentType);
    }

    public void AddHtmlResponse(string url, string htmlContent)
    {
        AddResponse(url, HttpStatusCode.OK, System.Text.Encoding.UTF8.GetBytes(htmlContent), "text/html");
    }

    public void AddPdfResponse(string url, byte[] pdfContent)
    {
        AddResponse(url, HttpStatusCode.OK, pdfContent, "application/pdf");
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri?.ToString() ?? string.Empty;

        if (_responses.TryGetValue(url, out var response))
        {
            var httpResponse = new HttpResponseMessage(response.statusCode)
            {
                Content = new ByteArrayContent(response.content)
            };
            httpResponse.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(response.contentType);
            return Task.FromResult(httpResponse);
        }

        // Return 404 for unregistered URLs
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent($"Mock handler: No response configured for URL: {url}")
        });
    }
}
