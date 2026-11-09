using Mapa.Core.Import;
using Mapa.Core.Models;

namespace Mapa.Core.Tests.Import;

public class ExcelEventReaderTests
{
    private static string SampleExcelPath =>
        Path.Combine(AppContext.BaseDirectory, "TestData", "Exemplo_Eventos_MPleno.xlsx");

    [Fact]
    public void Read_ParsesAllEightyEventsWithNoRowErrors()
    {
        var result = ExcelEventReader.Read(SampleExcelPath);

        Assert.Empty(result.RowErrors);
        Assert.Equal(80, result.Events.Count);
    }

    [Fact]
    public void Read_CategorizesEventsByEventType()
    {
        var result = ExcelEventReader.Read(SampleExcelPath);

        var voltageCount = result.Events.Count(e => e.Category == EventCategory.Voltage);
        var frequencyCount = result.Events.Count(e => e.Category == EventCategory.Frequency);

        Assert.Equal(55, voltageCount);
        Assert.Equal(25, frequencyCount);
        Assert.Equal(result.Events.Count, voltageCount + frequencyCount);
    }

    [Fact]
    public void Read_PreservesQualityStringWithLeadingZeros()
    {
        var result = ExcelEventReader.Read(SampleExcelPath);

        Assert.All(result.Events, e => Assert.Equal(8, e.QualityRaw.Length));
        Assert.Contains(result.Events, e => e.QualityRaw == "00000000");
    }

    [Fact]
    public void Read_ParsesStartAndEndTimeAsSameDayTimeOnly()
    {
        var result = ExcelEventReader.Read(SampleExcelPath);

        Assert.All(result.Events, e => Assert.True(e.EndTimestamp >= e.StartTimestamp));
    }

    [Fact]
    public void Read_ThreePmusHaveNoEvents()
    {
        var result = ExcelEventReader.Read(SampleExcelPath);
        var idNamesWithEvents = result.Events.Select(e => e.IdName).Distinct().ToHashSet();

        string[] expectedIdle =
        {
            "TR 500 - 34P5 kV COMPLEXO FUTURA T2",
            "CURRAL PIAUI II - SAO BASILIO C L5",
            "S V PALMAR 2 TF4",
        };

        foreach (var idName in expectedIdle)
            Assert.DoesNotContain(idName, idNamesWithEvents);
    }
}
