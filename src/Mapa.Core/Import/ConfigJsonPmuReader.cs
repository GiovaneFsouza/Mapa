using System.Text.Json;
using Mapa.Core.Models;

namespace Mapa.Core.Import;

/// <summary>Reads PMU metadata (id, label, lat/lon, kv) from the event-detector config.json.</summary>
public static class ConfigJsonPmuReader
{
    private static readonly IReadOnlyDictionary<string, string> StateToRegion =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AC"] = "N",  ["AM"] = "N",  ["AP"] = "N",  ["PA"] = "N",
            ["RO"] = "N",  ["RR"] = "N",  ["TO"] = "N",
            ["AL"] = "NE", ["BA"] = "NE", ["CE"] = "NE", ["MA"] = "NE",
            ["PB"] = "NE", ["PE"] = "NE", ["PI"] = "NE", ["RN"] = "NE", ["SE"] = "NE",
            ["DF"] = "CO", ["GO"] = "CO", ["MS"] = "CO", ["MT"] = "CO",
            ["ES"] = "SE", ["MG"] = "SE", ["RJ"] = "SE", ["SP"] = "SE",
            ["PR"] = "S",  ["RS"] = "S",  ["SC"] = "S",
        };

    public static IReadOnlyList<Pmu> Read(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Read(stream);
    }

    public static IReadOnlyList<Pmu> Read(Stream jsonStream)
    {
        using var doc = JsonDocument.Parse(jsonStream);
        var root = doc.RootElement;

        if (!root.TryGetProperty("pmus", out var pmusEl))
            return Array.Empty<Pmu>();

        var pmus = new List<Pmu>();
        foreach (var el in pmusEl.EnumerateArray())
        {
            var id = GetString(el, "id");
            if (string.IsNullOrEmpty(id))
                continue;

            // Entries without geographic coordinates cannot be shown on the map
            if (!el.TryGetProperty("latitude", out var latEl) || !el.TryGetProperty("longitude", out var lonEl))
                continue;

            var label = GetString(el, "label");
            if (string.IsNullOrEmpty(label))
                label = id;

            var kvLl = el.TryGetProperty("kv_ll", out var kvEl) ? kvEl.GetDouble() : 0.0;
            var lat = latEl.GetDouble();
            var lon = lonEl.GetDouble();

            var state = ParseState(label);
            var area = state.Length > 0 && StateToRegion.TryGetValue(state, out var r) ? r : string.Empty;

            pmus.Add(new Pmu(id, label, (int)(kvLl * 1000), area, state, label, lat, lon));
        }

        return pmus;
    }

    private static string GetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) ? p.GetString()?.Trim() ?? string.Empty : string.Empty;

    /// <summary>Extracts the state abbreviation from the end of a label like "UFJF - Juiz de Fora MG".</summary>
    private static string ParseState(string label)
    {
        var parts = label.Split(' ');
        if (parts.Length == 0)
            return string.Empty;
        var last = parts[^1];
        return last.Length == 2
               && char.IsLetter(last[0]) && char.IsLetter(last[1])
               && last == last.ToUpperInvariant()
            ? last
            : string.Empty;
    }
}
