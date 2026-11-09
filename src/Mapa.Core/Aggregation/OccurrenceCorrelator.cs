using System.Globalization;
using Mapa.Core.Models;

namespace Mapa.Core.Aggregation;

/// <summary>
/// Groups events that are the same physical disturbance using a dual-constraint greedy algorithm:
/// a new event extends the current group only when BOTH (a) the gap from the previous event is
/// within gapMax AND (b) the total span from the group's first event is within rangeMax. Violating
/// either constraint starts a new group.
///
/// Voltage and frequency are correlated separately: they are different physical phenomena and the
/// app keeps the two grandezas apart everywhere else.
/// </summary>
public static class OccurrenceCorrelator
{
    public static IReadOnlyList<Occurrence> Correlate(
        IEnumerable<PmuEvent> events, EventCategory category,
        TimeSpan gapMax, TimeSpan rangeMax, int minimumPmus = 1)
    {
        if (gapMax < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(gapMax), gapMax, "gapMax não pode ser negativo.");
        if (rangeMax < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(rangeMax), rangeMax, "rangeMax não pode ser negativo.");

        var ordered = events
            .Where(e => e.Category == category)
            .OrderBy(e => e.StartTimestamp)
            .ThenBy(e => e.IdName, StringComparer.Ordinal)
            .ToList();

        var occurrences = new List<Occurrence>();
        if (ordered.Count == 0) return occurrences;

        var group      = new List<PmuEvent> { ordered[0] };
        var groupStart = ordered[0].StartTimestamp;
        var groupPrev  = ordered[0].StartTimestamp;

        for (var i = 1; i < ordered.Count; i++)
        {
            var ev   = ordered[i];
            var gap  = ev.StartTimestamp - groupPrev;
            var span = ev.StartTimestamp - groupStart;

            if (gap <= gapMax && span <= rangeMax)
            {
                group.Add(ev);
                groupPrev = ev.StartTimestamp;
            }
            else
            {
                if (DistinctPmuCount(group) >= minimumPmus)
                    occurrences.Add(new Occurrence(BuildId(category, groupStart), category, groupStart, group));
                group      = [ev];
                groupStart = ev.StartTimestamp;
                groupPrev  = ev.StartTimestamp;
            }
        }

        if (DistinctPmuCount(group) >= minimumPmus)
            occurrences.Add(new Occurrence(BuildId(category, groupStart), category, groupStart, group));

        return occurrences;
    }

    private static int DistinctPmuCount(List<PmuEvent> events) =>
        events.Select(e => e.IdName).Distinct(StringComparer.Ordinal).Count();

    private static string BuildId(EventCategory category, DateTime anchor)
    {
        var prefix = category == EventCategory.Voltage ? "VTCD" : "FREQ";
        return $"{prefix}_{anchor.ToString("yyyyMMdd_HHmmss.fff", CultureInfo.InvariantCulture)}";
    }
}
