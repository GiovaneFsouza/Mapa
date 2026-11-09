using Mapa.Core.Models;

namespace Mapa.Core.Aggregation;

/// <summary>
/// Joins filtered events (by idName) to known PMUs and computes one metric value per PMU
/// for the requested category + mode. Metric definitions (confirmed with the user):
///   - Severity: the raw (signed) extreme_value of whichever event has the largest absolute
///     deviation from nominal (1.0 pu / 60 Hz) — lets the value land correctly on either side
///     of a diverging color scale (sag vs swell, under vs over frequency).
///   - Quantity: count of matching events.
///   - Duration: the single longest event's duration (not summed), in ms for VTCD and in
///     seconds for frequency — the units each grandeza is normally reported in.
///
/// idName is NOT guaranteed unique in real XML files: a transmission line is commonly monitored
/// by a PMU at each terminal substation, both sharing the line's name but with different
/// lat/lon. When that happens, the same matching events fan out to a PmuAggregate per physical
/// location, so the line's events are visible on the map at both ends.
/// </summary>
public sealed class AggregationEngine
{
    public IReadOnlyList<PmuAggregate> Aggregate(
        IEnumerable<PmuEvent> filteredEvents,
        IReadOnlyList<Pmu> pmus,
        EventCategory category,
        MetricMode mode)
    {
        var pmusByIdName = pmus
            .GroupBy(p => p.IdName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        var nominal = DomainConstants.NominalFor(category);

        var groups = filteredEvents
            .Where(e => e.Category == category)
            .GroupBy(e => e.IdName, StringComparer.Ordinal);

        var results = new List<PmuAggregate>();
        foreach (var group in groups)
        {
            if (!pmusByIdName.TryGetValue(group.Key, out var matchingPmus))
                continue; // unmatched idName: surfaced separately by IdNameValidator, never plotted

            var matchingEvents = group.ToList();
            var metricValue = mode switch
            {
                MetricMode.Severity => SeverityValue(matchingEvents, nominal),
                MetricMode.Quantity => matchingEvents.Count,
                MetricMode.Duration => DurationOf(matchingEvents, category),
                _ => throw new ArgumentOutOfRangeException(nameof(mode)),
            };

            foreach (var pmu in matchingPmus)
                results.Add(new PmuAggregate(pmu, category, mode, metricValue, matchingEvents));
        }

        return results;
    }

    /// <summary>Longest single event, in the unit that suits the grandeza (see
    /// <see cref="DomainConstants.DurationUnitFor"/>): milliseconds for VTCD, seconds for frequency.</summary>
    private static double DurationOf(IReadOnlyList<PmuEvent> events, EventCategory category)
    {
        var longest = events.Max(e => e.Duration);
        return category == EventCategory.Voltage ? longest.TotalMilliseconds : longest.TotalSeconds;
    }

    private static double SeverityValue(IReadOnlyList<PmuEvent> events, double nominal)
    {
        // Return the actual extreme_value (e.g. 0.8283 pu / 1.1414 pu).
        // ThresholdScale handles direction via L1 > L2 (inverted for Sub) or L1 < L2 (Sobre).
        var worst = events.MaxBy(e => Math.Abs(e.ExtremeValue - nominal))!;
        return worst.ExtremeValue;
    }
}
