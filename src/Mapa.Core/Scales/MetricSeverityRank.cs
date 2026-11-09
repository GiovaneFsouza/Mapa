using Mapa.Core.Aggregation;
using Mapa.Core.Models;

namespace Mapa.Core.Scales;

/// <summary>
/// Maps a PMU's metric value onto a "how bad is this" ranking where higher always means worse,
/// so a map cluster can adopt the color of its worst child (the industry-standard worst-case
/// aggregation for operational monitoring: averaging colors washes sags and swells into a
/// meaningless mid-tone, hiding the very events the operator needs to spot when zoomed out).
///
/// What counts as "worst" differs per mode: for Quantity/Duration the raw value already grows
/// with severity, but for Severity the value is a signed measurement whose badness is its
/// distance from nominal (1.0 pu / 60 Hz) in either direction.
/// </summary>
public static class MetricSeverityRank
{
    // All modes now use "larger = worse": Severity returns deviation (already unsigned),
    // Quantity/Duration are naturally monotone, Undefined is treated as Quantity.
    public static double For(double metricValue, MetricMode mode, EventCategory category) => metricValue;
}
