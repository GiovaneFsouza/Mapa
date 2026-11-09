using Mapa.Core.Import;

namespace Mapa.Core.Tests.Import;

public class XmlPmuReaderTests
{
    private static string SampleXmlPath =>
        Path.Combine(AppContext.BaseDirectory, "TestData", "OH2_ONS_BSB_MPleno.xml");

    [Fact]
    public void Read_ParsesAllSeventeenPmus()
    {
        var pmus = XmlPmuReader.Read(SampleXmlPath);

        Assert.Equal(17, pmus.Count);
    }

    [Fact]
    public void Read_HandlesSmsf2DefaultNamespace_AndParsesLatLon()
    {
        var pmus = XmlPmuReader.Read(SampleXmlPath);

        var pmu = pmus.Single(p => p.IdName == "LT OUROLANDIA II - CAMPO LARGO C1");

        Assert.Equal(-10.8745, pmu.Lat, precision: 4);
        Assert.Equal(-41.3399, pmu.Lon, precision: 4);
        Assert.Equal(230000, pmu.VoltLevel);
        Assert.Equal("BA", pmu.State);
    }

    [Fact]
    public void Read_ParsesAccentedIdNamesExactly()
    {
        var pmus = XmlPmuReader.Read(SampleXmlPath);

        Assert.Contains(pmus, p => p.IdName == "LT JOÃO CÂMARA III - MONTE DO RONCA C1");
        Assert.Contains(pmus, p => p.IdName == "LT JANAÚBA 3 - UFV JANAÚBA");
    }

    [Fact]
    public void Read_DetectsPmusSharingIdenticalCoordinates()
    {
        var pmus = XmlPmuReader.Read(SampleXmlPath);

        var bomNomePmus = pmus.Where(p => p.Station == "SUB BOM NOME").ToList();

        Assert.Equal(2, bomNomePmus.Count);
        Assert.Equal(bomNomePmus[0].Lat, bomNomePmus[1].Lat);
        Assert.Equal(bomNomePmus[0].Lon, bomNomePmus[1].Lon);
    }
}
