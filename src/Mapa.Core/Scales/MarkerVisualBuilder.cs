using Mapa.Core.Aggregation;
using Mapa.Core.Models;

namespace Mapa.Core.Scales;

public sealed record MarkerVisual(string ColorHex, double RadiusPx);

/// <summary>
/// Single source of truth turning a PmuAggregate's MetricValue into color+size. Color reads the
/// raw MetricValue against the mode's ScaleRange (so Severity lands correctly on either side of
/// the diverging scale). Radius is fixed: encoding the same value in both color and size read as
/// redundant/cluttered on the map, so size no longer varies with the metric — color plus the
/// legend/ranking/popup already convey magnitude.
/// </summary>
public static class MarkerVisualBuilder
{
    public const double DefaultRadiusPx = 9;

    public static MarkerVisual Build(
        double metricValue, ThresholdScale scale,
        double radiusPx = DefaultRadiusPx)
    {
        return new MarkerVisual(scale.ColorFor(metricValue), radiusPx);
    }
}
