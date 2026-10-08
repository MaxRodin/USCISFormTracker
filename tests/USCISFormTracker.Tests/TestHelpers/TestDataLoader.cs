namespace USCISFormTracker.Tests.TestHelpers;

/// <summary>
/// Helper class for loading test data files
/// </summary>
public static class TestDataLoader
{
    private static string GetTestDataPath()
    {
        // Get the test assembly location and navigate to TestData folder
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(baseDir, "TestData");
    }

    public static string LoadHtmlFile(string fileName)
    {
        var path = Path.Combine(GetTestDataPath(), "Html", fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Test HTML file not found: {path}");
        }
        return File.ReadAllText(path);
    }

    public static byte[] LoadPdfFile(string fileName)
    {
        var path = Path.Combine(GetTestDataPath(), "Pdf", fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Test PDF file not found: {path}");
        }
        return File.ReadAllBytes(path);
    }

    public static string GetHtmlPath(string fileName)
    {
        return Path.Combine(GetTestDataPath(), "Html", fileName);
    }

    public static string GetPdfPath(string fileName)
    {
        return Path.Combine(GetTestDataPath(), "Pdf", fileName);
    }
}
