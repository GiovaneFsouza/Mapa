using Mapa.Core.Scales;

namespace Mapa.Core.Tests.Scales;

public class MarkerVisualBuilderTests
{
    private static ThresholdScale VoltageScale => new(0.70, 0.90, IsInverted: true);

    [Fact]
    public void RadiusIsFixed_RegardlessOfMetricValue()
    {
        var mild   = MarkerVisualBuilder.Build(0.95, VoltageScale);
        var severe = MarkerVisualBuilder.Build(0.50, VoltageScale);

        Assert.Equal(MarkerVisualBuilder.DefaultRadiusPx, mild.RadiusPx);
        Assert.Equal(MarkerVisualBuilder.DefaultRadiusPx, severe.RadiusPx);
    }

    [Fact]
    public void Severity_ColorDependsOnThresholdScale()
    {
        // value above L2 (0.90) on an inverted scale → green (normal)
        var normal = MarkerVisualBuilder.Build(0.95, VoltageScale);
        // value below L1 (0.70) on an inverted scale → red (severe)
        var severe = MarkerVisualBuilder.Build(0.50, VoltageScale);

        Assert.NotEqual(normal.ColorHex, severe.ColorHex);
    }
}
