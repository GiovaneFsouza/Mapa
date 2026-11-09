using Mapa.Core.Aggregation;
using Mapa.Core.Models;
using Mapa.Core.Scales;

namespace Mapa.Core.Tests.Scales;

public class MetricSeverityRankTests
{
    [Fact]
    public void Severity_ReturnsRawMetricValue()
    {
        // MetricSeverityRank passes the raw value through so the cluster layer can compare
        // children without knowing each metric's notion of "worst".
        var rank = MetricSeverityRank.For(0.50, MetricMode.Severity, EventCategory.Voltage);

        Assert.Equal(0.50, rank);
    }

    [Theory]
    [InlineData(MetricMode.Quantity)]
    [InlineData(MetricMode.Duration)]
    public void QuantityAndDuration_RankByRawValue(MetricMode mode)
    {
        Assert.True(
            MetricSeverityRank.For(16, mode, EventCategory.Voltage) >
            MetricSeverityRank.For(2, mode, EventCategory.Voltage));
    }
}
