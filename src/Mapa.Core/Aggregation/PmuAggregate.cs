using Mapa.Core.Models;

namespace Mapa.Core.Aggregation;

/// <summary>
/// One PMU's computed marker value for a given (category, mode, current filter). A PMU with
/// zero matching events never produces a PmuAggregate — absence from the result IS the
/// "no events -> not shown on the map" rule.
/// </summary>
public sealed record PmuAggregate(
    Pmu Pmu,
    EventCategory Category,
    MetricMode Mode,
    double MetricValue,
    IReadOnlyList<PmuEvent> MatchingEvents);
