using System.Globalization;
using System.Xml.Linq;
using Mapa.Core.Models;

namespace Mapa.Core.Import;

/// <summary>Reads PMU metadata (idName/lat/lon/etc) from a smsf2-schema XML file.</summary>
public static class XmlPmuReader
{
    private static readonly XNamespace Ns = "smsf2";

    public static IReadOnlyList<Pmu> Read(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Read(stream);
    }

    public static IReadOnlyList<Pmu> Read(Stream xmlStream)
    {
        var doc = XDocument.Load(xmlStream);
        var pmus = new List<Pmu>();

        foreach (var pmuEl in doc.Descendants(Ns + "pmu"))
        {
            var local = pmuEl.Element(Ns + "local");
            var idName = pmuEl.Element(Ns + "idName")?.Value.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(idName))
                continue;

            var fullName = pmuEl.Element(Ns + "fullName")?.Value.Trim() ?? idName;
            var voltLevel = ParseInt(pmuEl.Element(Ns + "voltLevel")?.Value);
            var area = local?.Element(Ns + "area")?.Value.Trim() ?? string.Empty;
            var state = local?.Element(Ns + "state")?.Value.Trim() ?? string.Empty;
            var station = local?.Element(Ns + "station")?.Value.Trim() ?? string.Empty;
            var lat = ParseDouble(local?.Element(Ns + "lat")?.Value);
            var lon = ParseDouble(local?.Element(Ns + "lon")?.Value);

            pmus.Add(new Pmu(idName, fullName, voltLevel, area, state, station, lat, lon));
        }

        return pmus;
    }

    private static int ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;

    private static double ParseDouble(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : 0.0;
}
