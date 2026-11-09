using Mapa.Core.Aggregation;
using Mapa.Core.Models;

namespace Mapa.Core.Tests.Aggregation;

public class IdNameValidatorTests
{
    private static readonly DateOnly Day = new(2026, 1, 10);

    private static PmuEvent Ev(string idName) =>
        PmuEvent.Create($"{idName}_1", idName, 2, "AMT", Day, new TimeOnly(10, 0, 0), new TimeOnly(10, 0, 1), 0.8, "00000000");

    [Fact]
    public void Validate_ReportsIdNamesMissingFromXml()
    {
        var pmus = new[] { new Pmu("PMU_A", "PMU A", 230000, "NE", "BA", "SUB A", -10, -40) };
        var events = new[] { Ev("PMU_A"), Ev("PMU_GHOST") };

        var result = IdNameValidator.Validate(events, pmus);

        Assert.True(result.HasMismatches);
        Assert.Equal(new[] { "PMU_GHOST" }, result.UnmatchedIdNames);
    }

    [Fact]
    public void Validate_NoMismatches_WhenAllIdNamesKnown()
    {
        var pmus = new[] { new Pmu("PMU_A", "PMU A", 230000, "NE", "BA", "SUB A", -10, -40) };
        var events = new[] { Ev("PMU_A") };

        var result = IdNameValidator.Validate(events, pmus);

        Assert.False(result.HasMismatches);
        Assert.Empty(result.UnmatchedIdNames);
    }
}
