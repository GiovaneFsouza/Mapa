using Mapa.Core.Settings;

namespace Mapa.Core.Tests.Settings;

public class SettingsServiceTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(Path.GetTempPath(), $"mapa-settings-test-{Guid.NewGuid():N}.json");

    [Fact]
    public void Load_WhenFileMissing_ReturnsDefaults()
    {
        var service = new SettingsService(_tempFile);

        var settings = service.Load();

        Assert.Null(settings.BdedFolderPath);
        Assert.Null(settings.ConfigJsonPath);
        Assert.Equal(500.0, settings.VtcdGapMaxMs);
        Assert.Equal(1000.0, settings.VtcdRangeMaxMs);
        Assert.Equal(2.0, settings.FrequencyCorrelationWindowSeconds);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsValues()
    {
        var service = new SettingsService(_tempFile);
        var settings = new AppSettings
        {
            BdedFolderPath  = @"C:\dados\BDED",
            ConfigJsonPath  = @"C:\dados\config.json",
            VtcdGapMaxMs    = 300.0,
            VtcdRangeMaxMs  = 750.0,
        };

        service.Save(settings);
        var loaded = service.Load();

        Assert.Equal(settings.BdedFolderPath, loaded.BdedFolderPath);
        Assert.Equal(settings.ConfigJsonPath,  loaded.ConfigJsonPath);
        Assert.Equal(settings.VtcdGapMaxMs,    loaded.VtcdGapMaxMs);
        Assert.Equal(settings.VtcdRangeMaxMs,  loaded.VtcdRangeMaxMs);
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
            File.Delete(_tempFile);
    }
}
