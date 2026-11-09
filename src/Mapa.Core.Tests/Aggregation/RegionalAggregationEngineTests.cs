using Mapa.Core.Aggregation;
using Mapa.Core.Models;

namespace Mapa.Core.Tests.Aggregation;

public class RegionalAggregationEngineTests
{
    private static readonly Pmu PmuNe1 = new("PMU_NE_1", "PMU NE 1", 230000, "NE", "BA", "SUB A", -10, -40);
    private static readonly Pmu PmuNe2 = new("PMU_NE_2", "PMU NE 2", 230000, "NE", "PE", "SUB B", -8, -38);
    private static readonly Pmu PmuSe1 = new("PMU_SE_1", "PMU SE 1", 500000, "SE", "MG", "SUB C", -19, -44);

    private static readonly DateOnly Day = new(2026, 1, 10);

    private static PmuEvent Ev(string idName, string eventType, int sourceId = 2) =>
        PmuEvent.Create($"{idName}_{Guid.NewGuid():N}", idName, sourceId, eventType, Day,
            new TimeOnly(10, 0, 0), new TimeOnly(10, 0, 1), eventType == "EMT" ? 1.1 : 0.8, "00000000");

    [Fact]
    public void Aggregate_GroupsByPmuAreaAndSplitsDirection()
    {
        var events = new List<PmuEvent>
        {
            Ev("PMU_NE_1", "AMT"), // NE, Low
            Ev("PMU_NE_1", "AMT"), // NE, Low
            Ev("PMU_NE_2", "EMT"), // NE, High
            Ev("PMU_SE_1", "UNDEFINED"), // SE, Indeterminate
        };

        var result = new RegionalAggregationEngine().Aggregate(events, new[] { PmuNe1, PmuNe2, PmuSe1 }, EventCategory.Voltage);

        var ne = Assert.Single(result, r => r.Region == "NE");
        Assert.Equal(2, ne.LowCount);
        Assert.Equal(1, ne.HighCount);
        Assert.Equal(3, ne.Total);

        var se = Assert.Single(result, r => r.Region == "SE");
        Assert.Equal(1, se.IndeterminateCount);
        Assert.Equal(1, se.Total);
    }

    [Fact]
    public void Aggregate_OrdersByTotalDescending()
    {
        var events = new List<PmuEvent>
        {
            Ev("PMU_SE_1", "AMT"),
            Ev("PMU_NE_1", "AMT"),
            Ev("PMU_NE_1", "AMT"),
            Ev("PMU_NE_2", "AMT"),
        };

        var result = new RegionalAggregationEngine().Aggregate(events, new[] { PmuNe1, PmuNe2, PmuSe1 }, EventCategory.Voltage);

        Assert.Equal("NE", result[0].Region);
        Assert.Equal("SE", result[1].Region);
    }

    [Fact]
    public void Aggregate_DuplicateIdNameAcrossRegions_CountsEventOnceUnderFirstRegion()
    {
        // Same idName, two terminal PMUs in different regions (NE and SE) -- must not double count.
        var terminalNe = new Pmu("LT A-B", "LT A-B", 500000, "NE", "BA", "SUB A", -10, -40);
        var terminalSe = new Pmu("LT A-B", "LT A-B", 500000, "SE", "MG", "SUB B", -19, -44);
        var events = new List<PmuEvent> { Ev("LT A-B", "AMT") };

        var result = new RegionalAggregationEngine().Aggregate(events, new[] { terminalNe, terminalSe }, EventCategory.Voltage);

        Assert.Equal(1, result.Sum(r => r.Total));
    }

    [Fact]
    public void Aggregate_IgnoresOtherCategoryAndUnknownIdName()
    {
        var events = new List<PmuEvent>
        {
            Ev("PMU_NE_1", "AMT"),
            PmuEvent.Create("f1", "PMU_NE_1", 1, "Underfrequency", Day, new TimeOnly(9, 0), new TimeOnly(9, 0, 1), 59.5, "00000000"),
            Ev("PMU_GHOST", "AMT"),
        };

        var result = new RegionalAggregationEngine().Aggregate(events, new[] { PmuNe1 }, EventCategory.Voltage);

        var ne = Assert.Single(result);
        Assert.Equal(1, ne.Total);
    }
}
