using Mapa.Core.Scales;

namespace Mapa.Core.Tests.Scales;

public class ScaleRangeTests
{
    [Theory]
    [InlineData(0.0, 0, 1.2, 0.0)]
    [InlineData(0.6, 0, 1.2, 0.5)]
    [InlineData(1.2, 0, 1.2, 1.0)]
    [InlineData(-5.0, 0, 1.2, 0.0)] // clamped below min
    [InlineData(5.0, 0, 1.2, 1.0)]  // clamped above max
    public void Normalize_ClampsAndScalesLinearly(double value, double min, double max, double expectedT)
    {
        var range = new ScaleRange(min, max);

        Assert.Equal(expectedT, range.Normalize(value), precision: 6);
    }

    [Fact]
    public void From_ComputesTightestRangeCoveringValues()
    {
        var range = ScaleRange.From(new[] { 3.0, 1.0, 5.0 });

        Assert.Equal(1.0, range.Min);
        Assert.Equal(5.0, range.Max);
    }

    [Fact]
    public void From_EmptyCollection_FallsBackToZeroOne()
    {
        var range = ScaleRange.From(Array.Empty<double>());

        Assert.Equal(0.0, range.Min);
        Assert.Equal(1.0, range.Max);
    }
}
