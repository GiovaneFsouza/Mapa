using Mapa.Core.Aggregation;
using Mapa.Core.Models;

namespace Mapa.Core.Tests.Aggregation;

public class AggregationEngineTests
{
    private static readonly Pmu PmuA = new("PMU_A", "PMU A", 230000, "NE", "BA", "SUB A", -10, -40);
    private static readonly Pmu PmuB = new("PMU_B", "PMU B", 230000, "NE", "BA", "SUB B", -11, -41);

    private static readonly DateOnly Day = new(2026, 1, 10);

    private static PmuEvent VoltageEvent(string idName, string eventType, double extremeValue, TimeOnly start, TimeOnly end) =>
        PmuEvent.Create($"{idName}_{start:HHmmss}", idName, 2, eventType, Day, start, end, extremeValue, "00000000");

    [Fact]
    public void Aggregate_Severity_UsesRawSignedValueOfMostDeviatedEvent()
    {
        // Sag at 0.50 pu deviates 0.50 from nominal; swell at 1.10 pu deviates only 0.10.
        var events = new List<PmuEvent>
        {
            VoltageEvent("PMU_A", "AMT", 0.50, new TimeOnly(10, 0, 0), new TimeOnly(10, 0, 1)),
            VoltageEvent("PMU_A", "EMT", 1.10, new TimeOnly(11, 0, 0), new TimeOnly(11, 0, 1)),
        };

        var result = new AggregationEngine().Aggregate(events, new[] { PmuA }, EventCategory.Voltage, MetricMode.Severity);

        var aggregate = Assert.Single(result);
        Assert.Equal(0.50, aggregate.MetricValue); // raw signed value of the WORSE event, not its deviation
    }

    [Fact]
    public void Aggregate_Severity_SwellWins_KeepsPositiveRawValueAboveNominal()
    {
        var events = new List<PmuEvent>
        {
            VoltageEvent("PMU_A", "AMT", 0.92, new TimeOnly(10, 0, 0), new TimeOnly(10, 0, 1)),
            VoltageEvent("PMU_A", "ETT", 1.18, new TimeOnly(11, 0, 0), new TimeOnly(11, 0, 1)),
        };

        var result = new AggregationEngine().Aggregate(events, new[] { PmuA }, EventCategory.Voltage, MetricMode.Severity);

        Assert.Equal(1.18, Assert.Single(result).MetricValue);
    }

    [Fact]
    public void Aggregate_Duration_UsesLongestSingleEvent_NotSum()
    {
        var events = new List<PmuEvent>
        {
            VoltageEvent("PMU_A", "AMT", 0.8, new TimeOnly(10, 0, 0), new TimeOnly(10, 0, 10)),  // 10s
            VoltageEvent("PMU_A", "ATT", 0.7, new TimeOnly(11, 0, 0), new TimeOnly(11, 3, 20)),  // 200s
            VoltageEvent("PMU_A", "AMT", 0.9, new TimeOnly(12, 0, 0), new TimeOnly(12, 0, 5)),   // 5s
        };

        var result = new AggregationEngine().Aggregate(events, new[] { PmuA }, EventCategory.Voltage, MetricMode.Duration);

        Assert.Equal(200_000.0, Assert.Single(result).MetricValue); // VTCD é reportado em ms
    }

    [Fact]
    public void Aggregate_Duration_VoltageInMilliseconds_FrequencyInSeconds()
    {
        // Um afundamento típico dura dezenas de ms; em segundos ele aparecia como "0,0 s".
        var sag = PmuEvent.Create("v1", "PMU_A", 2, "AMT", Day,
            new TimeOnly(10, 0, 0), new TimeOnly(10, 0, 0, 42), 0.86, "00000000");
        var underFrequency = PmuEvent.Create("f1", "PMU_A", 1, "Underfrequency", Day,
            new TimeOnly(11, 0, 0), new TimeOnly(11, 0, 45), 59.2, "00000000");

        var engine = new AggregationEngine();
        var voltage = engine.Aggregate(new[] { sag }, new[] { PmuA }, EventCategory.Voltage, MetricMode.Duration);
        var frequency = engine.Aggregate(new[] { underFrequency }, new[] { PmuA }, EventCategory.Frequency, MetricMode.Duration);

        Assert.Equal(42.0, Assert.Single(voltage).MetricValue);
        Assert.Equal(45.0, Assert.Single(frequency).MetricValue);
    }

    [Theory]
    [InlineData(EventCategory.Voltage, "ms")]
    [InlineData(EventCategory.Frequency, "s")]
    public void DurationUnit_MatchesGrandeza(EventCategory category, string expected)
    {
        Assert.Equal(expected, DomainConstants.DurationUnitFor(category));
    }

    [Fact]
    public void Aggregate_Quantity_CountsMatchingEvents()
    {
        var events = Enumerable.Range(0, 4)
            .Select(i => VoltageEvent("PMU_A", "AMT", 0.8, new TimeOnly(10, i, 0), new TimeOnly(10, i, 1)))
            .ToList();

        var result = new AggregationEngine().Aggregate(events, new[] { PmuA }, EventCategory.Voltage, MetricMode.Quantity);

        Assert.Equal(4, Assert.Single(result).MetricValue);
    }

    [Fact]
    public void Aggregate_PmuWithNoMatchingEvents_IsExcludedFromResult()
    {
        var events = new List<PmuEvent>
        {
            VoltageEvent("PMU_A", "AMT", 0.8, new TimeOnly(10, 0, 0), new TimeOnly(10, 0, 1)),
        };

        var result = new AggregationEngine().Aggregate(events, new[] { PmuA, PmuB }, EventCategory.Voltage, MetricMode.Quantity);

        Assert.Single(result);
        Assert.DoesNotContain(result, a => a.Pmu.IdName == "PMU_B");
    }

    [Fact]
    public void Aggregate_EventWithUnknownIdName_IsSilentlyExcluded()
    {
        var events = new List<PmuEvent>
        {
            VoltageEvent("PMU_UNKNOWN", "AMT", 0.8, new TimeOnly(10, 0, 0), new TimeOnly(10, 0, 1)),
        };

        var result = new AggregationEngine().Aggregate(events, new[] { PmuA }, EventCategory.Voltage, MetricMode.Quantity);

        Assert.Empty(result);
    }

    [Fact]
    public void Aggregate_DuplicateIdName_FansOutToEachPhysicalLocation()
    {
        // Real ONS XML files reuse the same idName for a transmission line's two terminal PMUs,
        // each with different lat/lon (e.g. "LT ARAXA 3-NOVA PONTE" at both substations).
        var terminalOne = new Pmu("LT A-B", "LT A-B", 500000, "SE", "MG", "SUB A", -19.0, -47.0);
        var terminalTwo = new Pmu("LT A-B", "LT A-B", 500000, "SE", "MG", "SUB B", -19.5, -47.5);
        var events = new List<PmuEvent>
        {
            VoltageEvent("LT A-B", "AMT", 0.8, new TimeOnly(10, 0, 0), new TimeOnly(10, 0, 1)),
        };

        var result = new AggregationEngine().Aggregate(events, new[] { terminalOne, terminalTwo }, EventCategory.Voltage, MetricMode.Quantity);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, a => a.Pmu.Station == "SUB A");
        Assert.Contains(result, a => a.Pmu.Station == "SUB B");
        Assert.All(result, a => Assert.Equal(1, a.MetricValue));
    }

    [Fact]
    public void Aggregate_FiltersOutEventsFromOtherCategory()
    {
        var events = new List<PmuEvent>
        {
            VoltageEvent("PMU_A", "AMT", 0.8, new TimeOnly(10, 0, 0), new TimeOnly(10, 0, 1)),
            PmuEvent.Create("PMU_A_f", "PMU_A", 1, "Underfrequency", Day, new TimeOnly(9, 0, 0), new TimeOnly(9, 0, 5), 59.5, "00000000"),
        };

        var result = new AggregationEngine().Aggregate(events, new[] { PmuA }, EventCategory.Voltage, MetricMode.Quantity);

        Assert.Equal(1, Assert.Single(result).MetricValue);
    }
}
