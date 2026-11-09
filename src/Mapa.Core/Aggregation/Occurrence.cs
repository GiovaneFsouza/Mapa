using Mapa.Core.Models;

namespace Mapa.Core.Aggregation;

/// <summary>
/// A set of events judged to be the same physical disturbance: a fault in the grid is seen almost
/// simultaneously by several PMUs, so the Excel's one-row-per-PMU-per-detection becomes one
/// Occurrence spanning many PMUs (and often the three phases of a single PMU).
/// </summary>
public sealed record Occurrence(
    string Id,
    EventCategory Category,
    DateTime AnchorTimestamp,
    IReadOnlyList<PmuEvent> Events)
{
    /// <summary>Distinct PMUs that observed this disturbance (a PMU reporting phases A/B/C counts once).</summary>
    public IReadOnlyList<string> IdNames { get; } = Events
        .Select(e => e.IdName)
        .Distinct(StringComparer.Ordinal)
        .ToList();

    public int PmuCount => IdNames.Count;

    public DateTime EndTimestamp => Events.Max(e => e.EndTimestamp);

    /// <summary>Raw value of the event furthest from nominal — the disturbance's worst point.</summary>
    public double WorstExtremeValue
    {
        get
        {
            var nominal = DomainConstants.NominalFor(Category);
            return Events.MaxBy(e => Math.Abs(e.ExtremeValue - nominal))!.ExtremeValue;
        }
    }
}
