using Mapa.Core.Aggregation;
using Mapa.Core.Models;

namespace Mapa.Core.Tests.Aggregation;

public class OccurrenceCorrelatorTests
{
    private static readonly DateOnly Day = new(2026, 3, 12);
    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    // Convenience: gapMax = rangeMax = window (single-constraint semantics for most tests)
    private static IReadOnlyList<Occurrence> Correlate(IEnumerable<PmuEvent> events, EventCategory category, TimeSpan window)
        => OccurrenceCorrelator.Correlate(events, category, window, window);

    private static PmuEvent Voltage(string idName, TimeOnly start, int sourceId = 2, double value = 0.8) =>
        PmuEvent.Create($"{idName}_{start:HHmmssfff}_{sourceId}", idName, sourceId, "AMT", Day,
            start, start.Add(TimeSpan.FromMilliseconds(200)), value, "00000000");

    private static PmuEvent Frequency(string idName, TimeOnly start, double value = 59.5) =>
        PmuEvent.Create($"{idName}_{start:HHmmssfff}_f", idName, 1, "Underfrequency", Day,
            start, start.Add(TimeSpan.FromSeconds(5)), value, "00000000");

    [Fact]
    public void Correlate_GroupsEventsWithinWindowIntoOneOccurrence()
    {
        var events = new List<PmuEvent>
        {
            Voltage("PMU_A", new TimeOnly(10, 0, 0, 0)),
            Voltage("PMU_B", new TimeOnly(10, 0, 0, 300)),
            Voltage("PMU_C", new TimeOnly(10, 0, 0, 800)),
        };

        var result = Correlate(events, EventCategory.Voltage, OneSecond);

        var occurrence = Assert.Single(result);
        Assert.Equal(3, occurrence.PmuCount);
        Assert.Equal(3, occurrence.Events.Count);
    }

    [Fact]
    public void Correlate_EventsFurtherApartThanWindow_BecomeSeparateOccurrences()
    {
        var events = new List<PmuEvent>
        {
            Voltage("PMU_A", new TimeOnly(10, 0, 0, 0)),
            Voltage("PMU_B", new TimeOnly(10, 0, 5, 0)),
        };

        var result = Correlate(events, EventCategory.Voltage, OneSecond);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Correlate_RangeMaxPreventsLongCascades()
    {
        // Each event is 0.8s after the previous one. Pure gap-based chaining would swallow all four
        // into one occurrence spanning 2.4s; the rangeMax=1s constraint must split after the span
        // exceeds 1s (between B@0.8s and C@1.6s).
        var events = new List<PmuEvent>
        {
            Voltage("PMU_A", new TimeOnly(10, 0, 0, 0)),
            Voltage("PMU_B", new TimeOnly(10, 0, 0, 800)),
            Voltage("PMU_C", new TimeOnly(10, 0, 1, 600)),
            Voltage("PMU_D", new TimeOnly(10, 0, 2, 400)),
        };

        var result = Correlate(events, EventCategory.Voltage, OneSecond);

        Assert.Equal(2, result.Count);
        Assert.Equal(2, result[0].PmuCount); // A + B (B is within 1s of anchor A)
        Assert.Equal(2, result[1].PmuCount); // C anchors the next window, D joins it
    }

    [Fact]
    public void Correlate_EventExactlyOnWindowBoundary_IsIncluded()
    {
        var events = new List<PmuEvent>
        {
            Voltage("PMU_A", new TimeOnly(10, 0, 0, 0)),
            Voltage("PMU_B", new TimeOnly(10, 0, 1, 0)), // exactly Δt after the anchor
        };

        var result = Correlate(events, EventCategory.Voltage, OneSecond);

        Assert.Single(result);
    }

    [Fact]
    public void Correlate_ThreePhasesOfSamePmu_CountAsOnePmuButKeepAllEvents()
    {
        var events = new List<PmuEvent>
        {
            Voltage("PMU_A", new TimeOnly(10, 0, 0, 0), sourceId: 2),
            Voltage("PMU_A", new TimeOnly(10, 0, 0, 10), sourceId: 3),
            Voltage("PMU_A", new TimeOnly(10, 0, 0, 20), sourceId: 4),
        };

        var occurrence = Assert.Single(Correlate(events, EventCategory.Voltage, OneSecond));

        Assert.Equal(1, occurrence.PmuCount);
        Assert.Equal(3, occurrence.Events.Count);
    }

    [Fact]
    public void Correlate_NeverMixesVoltageAndFrequency()
    {
        var events = new List<PmuEvent>
        {
            Voltage("PMU_A", new TimeOnly(10, 0, 0, 0)),
            Frequency("PMU_B", new TimeOnly(10, 0, 0, 100)),
        };

        var voltage = Correlate(events, EventCategory.Voltage, OneSecond);
        var frequency = Correlate(events, EventCategory.Frequency, OneSecond);

        Assert.Single(voltage);
        Assert.Single(frequency);
        Assert.All(voltage, o => Assert.All(o.Events, e => Assert.Equal(EventCategory.Voltage, e.Category)));
        Assert.All(frequency, o => Assert.All(o.Events, e => Assert.Equal(EventCategory.Frequency, e.Category)));
    }

    [Fact]
    public void Occurrence_WorstExtremeValue_PicksFurthestFromNominalKeepingSign()
    {
        var events = new List<PmuEvent>
        {
            Voltage("PMU_A", new TimeOnly(10, 0, 0, 0), value: 0.95),
            Voltage("PMU_B", new TimeOnly(10, 0, 0, 100), value: 0.42), // worst sag
            Voltage("PMU_C", new TimeOnly(10, 0, 0, 200), value: 1.08),
        };

        var occurrence = Assert.Single(Correlate(events, EventCategory.Voltage, OneSecond));

        Assert.Equal(0.42, occurrence.WorstExtremeValue, precision: 6);
    }

    [Fact]
    public void Correlate_EmptyInput_ReturnsEmpty()
    {
        Assert.Empty(Correlate(new List<PmuEvent>(), EventCategory.Voltage, OneSecond));
    }
}
