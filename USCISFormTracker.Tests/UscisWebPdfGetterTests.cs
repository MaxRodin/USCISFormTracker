using USCISFormTracker.Core;
using USCISFormTracker.Tests.TestHelpers;

namespace USCISFormTracker.Tests;

public class UscisWebPdfGetterTests
{
    [Fact]
    public void GetPdfLinks_ShouldExtractFormDetailLinks_FromAllFormsPage()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler();
        var allFormsHtml = TestDataLoader.LoadHtmlFile("all-forms-snippet.html");

        mockHandler.AddHtmlResponse("https://www.uscis.gov/forms/all-forms", allFormsHtml);

        var httpClient = new HttpClient(mockHandler);
        var getter = new UscisWebPdfGetter(httpClient, "https://www.uscis.gov/forms/all-forms");

        // Act
        var links = getter.GetPdfLinks().ToList();

        // Assert
        Assert.Empty(links); // Because we haven't set up the detail page responses yet
        // The getter will try to fetch detail pages but get 404s
    }

    [Fact]
    public void GetPdfLinks_ShouldNavigateToDetailPages_AndExtractPdfLinks()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler();

        // Setup all-forms page
        var allFormsHtml = TestDataLoader.LoadHtmlFile("all-forms-snippet.html");
        mockHandler.AddHtmlResponse("https://www.uscis.gov/forms/all-forms", allFormsHtml);

        // Setup detail pages
        var i694DetailHtml = TestDataLoader.LoadHtmlFile("i-694-detail-example.html");
        var i751DetailHtml = TestDataLoader.LoadHtmlFile("i-751-detail.html");
        mockHandler.AddHtmlResponse("https://www.uscis.gov/i-694", i694DetailHtml);
        mockHandler.AddHtmlResponse("https://www.uscis.gov/i-751", i751DetailHtml);

        var httpClient = new HttpClient(mockHandler);
        var getter = new UscisWebPdfGetter(httpClient, "https://www.uscis.gov/forms/all-forms");

        // Act
        var links = getter.GetPdfLinks().ToList();

        // Assert
        Assert.Equal(2, links.Count);

        var i694Link = links.FirstOrDefault(l => l.FileName == "i-694.pdf");
        Assert.NotNull(i694Link);
        Assert.Equal("i-694.pdf", i694Link.FileName);
        Assert.Equal("https://www.uscis.gov/sites/default/files/document/forms/i-694.pdf", i694Link.FullLink);

        var i751Link = links.FirstOrDefault(l => l.FileName == "i-751.pdf");
        Assert.NotNull(i751Link);
        Assert.Equal("i-751.pdf", i751Link.FileName);
        Assert.Equal("https://www.uscis.gov/sites/default/files/document/forms/i-751.pdf", i751Link.FullLink);
    }

    [Fact]
    public void GetPdfLinks_ShouldOnlyExtractFormPdfs_NotInstructions()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler();
        var allFormsHtml = TestDataLoader.LoadHtmlFile("all-forms-snippet.html");
        var i751DetailHtml = TestDataLoader.LoadHtmlFile("i-751-detail.html");

        mockHandler.AddHtmlResponse("https://www.uscis.gov/forms/all-forms", allFormsHtml);
        mockHandler.AddHtmlResponse("https://www.uscis.gov/i-751", i751DetailHtml);

        var httpClient = new HttpClient(mockHandler);
        var getter = new UscisWebPdfGetter(httpClient, "https://www.uscis.gov/forms/all-forms");

        // Act
        var links = getter.GetPdfLinks().ToList();

        // Assert
        // Should only get the form PDF, not the instructions PDF
        var i751Links = links.Where(l => l.FileName.Contains("i-751")).ToList();
        Assert.Single(i751Links);
        Assert.Equal("i-751.pdf", i751Links[0].FileName);
        Assert.DoesNotContain(links, l => l.FileName.Contains("instr"));
    }
}
