using Mapa.Core.Settings;

namespace Mapa.Core.Tests.Settings;

public class XmlFolderScannerTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"mapa-xmlscan-test-{Guid.NewGuid():N}");

    [Fact]
    public void Scan_MissingFolder_ReturnsEmpty()
    {
        var result = XmlFolderScanner.Scan(Path.Combine(_tempDir, "does-not-exist"));

        Assert.Empty(result);
    }

    [Fact]
    public void Scan_ReturnsOnlyXmlFilesSortedByName()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(Path.Combine(_tempDir, "b.xml"), "<a/>");
        File.WriteAllText(Path.Combine(_tempDir, "a.xml"), "<a/>");
        File.WriteAllText(Path.Combine(_tempDir, "notes.txt"), "ignore me");

        var result = XmlFolderScanner.Scan(_tempDir);

        Assert.Equal(new[] { "a.xml", "b.xml" }, result.Select(e => e.FileName));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
