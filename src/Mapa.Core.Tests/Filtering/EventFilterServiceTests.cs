using Mapa.Core.Filtering;
using Mapa.Core.Models;

namespace Mapa.Core.Tests.Filtering;

public class EventFilterServiceTests
{
    private static PmuEvent Ev(DateOnly date, TimeOnly start, EventCategory category = EventCategory.Voltage)
    {
        var eventType = category == EventCategory.Voltage ? "AMT" : "Underfrequency";
        var extreme = category == EventCategory.Voltage ? 0.8 : 59.5;
        return PmuEvent.Create("id", "PMU_A", 2, eventType, date, start, start, extreme, "00000000");
    }

    [Fact]
    public void Filter_KeepsOnlyEventsWithinDateRange()
    {
        var inside = Ev(new DateOnly(2026, 3, 15), new TimeOnly(10, 0));
        var before = Ev(new DateOnly(2026, 1, 1), new TimeOnly(10, 0));
        var after = Ev(new DateOnly(2026, 6, 1), new TimeOnly(10, 0));

        var criteria = new FilterCriteria(
            new DateTime(2026, 3, 1), new DateTime(2026, 3, 31, 23, 59, 59), null);

        var result = EventFilterService.Filter(new[] { inside, before, after }, criteria);

        Assert.Equal(new[] { inside }, result);
    }

    [Fact]
    public void Filter_WithCategoryFilter_ExcludesOtherCategory()
    {
        var voltage = Ev(new DateOnly(2026, 3, 15), new TimeOnly(10, 0), EventCategory.Voltage);
        var frequency = Ev(new DateOnly(2026, 3, 15), new TimeOnly(11, 0), EventCategory.Frequency);

        var criteria = new FilterCriteria(
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), EventCategory.Voltage);

        var result = EventFilterService.Filter(new[] { voltage, frequency }, criteria);

        Assert.Equal(new[] { voltage }, result);
    }

    [Fact]
    public void Filter_NullCategoryFilter_KeepsBothCategories()
    {
        var voltage = Ev(new DateOnly(2026, 3, 15), new TimeOnly(10, 0), EventCategory.Voltage);
        var frequency = Ev(new DateOnly(2026, 3, 15), new TimeOnly(11, 0), EventCategory.Frequency);

        var criteria = new FilterCriteria(
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), null);

        var result = EventFilterService.Filter(new[] { voltage, frequency }, criteria);

        Assert.Equal(2, result.Count);
    }
}
