using Mapa.Core.Scales;

namespace Mapa.Core.Tests.Scales;

/// <summary>
/// Covers the colour rule used by the Eventos Detectados tab: the scale auto-fits each event and
/// runs over deviation from nominal, so the worst PMU is always red and the mildest blue, and a
/// lone PMU (no gradient to show) lands on the neutral green midpoint.
/// </summary>
public class EventSeverityScaleTests
{
    private readonly LinearColorScale _scale = new();

    private const string Blue = "#2166AC";
    private const string Green = "#2ECC71";
    private const string Red = "#E74C3C";

    [Fact]
    public void WorstDeviationIsRed_MildestIsBlue()
    {
        // Sag event: 0.40 pu deviates 0.60 from nominal (worst), 0.88 pu deviates 0.12 (mildest).
        var range = new ScaleRange(0.12, 0.60);

        Assert.Equal(Red, _scale.ToHexColor(0.60, range));
        Assert.Equal(Blue, _scale.ToHexColor(0.12, range));
    }

    [Fact]
    public void SwellEvent_AlsoPutsWorstOnRed()
    {
        // Deviation is signless, so an over-voltage event ranks the same way.
        var range = new ScaleRange(0.05, 0.18);

        Assert.Equal(Red, _scale.ToHexColor(0.18, range));   // 1.18 pu
        Assert.Equal(Blue, _scale.ToHexColor(0.05, range));  // 1.05 pu
    }

    [Fact]
    public void SinglePmu_DegenerateRange_IsNeutralGreen()
    {
        var deviation = 0.34;
        var range = new ScaleRange(deviation, deviation);

        Assert.Equal(Green, _scale.ToHexColor(deviation, range));
    }

    [Fact]
    public void MidRangeDeviation_IsGreen()
    {
        var range = new ScaleRange(0.0, 1.0);

        Assert.Equal(Green, _scale.ToHexColor(0.5, range));
    }

    [Fact]
    public void ColoursProgressFromBlueThroughGreenToRedAsSeverityRises()
    {
        var range = new ScaleRange(0.10, 0.50);

        var mild = _scale.ToHexColor(0.10, range);
        var middle = _scale.ToHexColor(0.30, range);
        var worst = _scale.ToHexColor(0.50, range);

        Assert.Equal(Blue, mild);
        Assert.Equal(Green, middle);
        Assert.Equal(Red, worst);
    }
}
