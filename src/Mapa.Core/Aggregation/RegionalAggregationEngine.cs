using Mapa.Core.Models;

namespace Mapa.Core.Aggregation;

/// <summary>
/// Groups filtered events by the region (Pmu.Area) of the PMU that detected them, counting
/// Low/High/Indeterminate directions within it. Used by the Quantidade mode's regional
/// breakdown, which replaces the per-PMU map (per-PMU counts alone weren't judged useful;
/// a regional, direction-split view is).
///
/// idName is NOT guaranteed unique (a transmission line's two terminal PMUs commonly share the
/// line's name with different lat/lon/area). Unlike AggregationEngine's per-location fan-out for
/// the map, each event here is counted exactly once — under the region of its idName's first
/// matching PMU record — so regional totals stay a true count of events, not inflated by
/// duplicate idName entries.
/// </summary>
public sealed class RegionalAggregationEngine
{
    private const string UnknownRegionLabel = "Não informado";

    public IReadOnlyList<RegionCount> Aggregate(
        IEnumerable<PmuEvent> filteredEvents, IReadOnlyList<Pmu> pmus, EventCategory category)
    {
        var regionByIdName = pmus
            .GroupBy(p => p.IdName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Area, StringComparer.Ordinal);

        var counts = new Dictionary<string, (int Low, int High, int Indeterminate)>(StringComparer.Ordinal);

        foreach (var ev in filteredEvents)
        {
            if (ev.Category != category)
                continue;
            if (!regionByIdName.TryGetValue(ev.IdName, out var area))
                continue;

            var region = string.IsNullOrWhiteSpace(area) ? UnknownRegionLabel : area;
            var current = counts.TryGetValue(region, out var existing) ? existing : (0, 0, 0);

            current = ev.Direction switch
            {
                EventDirection.Low => (current.Item1 + 1, current.Item2, current.Item3),
                EventDirection.High => (current.Item1, current.Item2 + 1, current.Item3),
                _ => (current.Item1, current.Item2, current.Item3 + 1),
            };

            counts[region] = current;
        }

        return counts
            .Select(kv => new RegionCount(kv.Key, kv.Value.Low, kv.Value.High, kv.Value.Indeterminate))
            .OrderByDescending(r => r.Total)
            .ThenBy(r => r.Region, StringComparer.Ordinal)
            .ToList();
    }
}
