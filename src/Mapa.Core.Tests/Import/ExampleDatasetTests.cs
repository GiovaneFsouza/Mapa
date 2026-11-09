using Mapa.Core.Import;
using Mapa.Core.Models;

namespace Mapa.Core.Tests.Import;

/// <summary>
/// Guards the sample case shipped in exemplos/caso-basico: it must stay readable by the same
/// readers the app uses, and every event must belong to a PMU the map can plot.
/// </summary>
public class ExampleDatasetTests
{
    private static string ExampleDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Mapa.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "exemplos", "caso-basico");
    }

    [Fact]
    public void ExampleConfig_HasPlottablePmus()
    {
        var pmus = ConfigJsonPmuReader.Read(Path.Combine(ExampleDir(), "config.json"));

        Assert.Equal(12, pmus.Count);
        Assert.All(pmus, p => Assert.False(string.IsNullOrEmpty(p.Area)));
    }

    [Fact]
    public void ExampleBded_ReadsWithoutErrors_AndMatchesConfig()
    {
        var dir = ExampleDir();
        var pmuIds = ConfigJsonPmuReader.Read(Path.Combine(dir, "config.json")).Select(p => p.IdName).ToHashSet();

        var result = CsvBdedReader.ReadFolder(Path.Combine(dir, "bded"));

        Assert.Empty(result.Errors);
        Assert.Equal(75, result.Events.Count);
        Assert.All(result.Events, e =>
        {
            Assert.Contains(e.IdName, pmuIds);
            Assert.True(e.Duration > TimeSpan.Zero);
        });
        Assert.Contains(result.Events, e => e.Category == EventCategory.Frequency);
        Assert.Contains(result.Events, e => !e.IsQualityOk);
    }
}
