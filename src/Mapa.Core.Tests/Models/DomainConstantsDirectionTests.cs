using Mapa.Core.Models;

namespace Mapa.Core.Tests.Models;

public class DomainConstantsDirectionTests
{
    [Theory]
    [InlineData("IMT", EventDirection.Low)]
    [InlineData("AMT", EventDirection.Low)]
    [InlineData("ITT", EventDirection.Low)]
    [InlineData("ATT", EventDirection.Low)]
    [InlineData("EMT", EventDirection.High)]
    [InlineData("ETT", EventDirection.High)]
    [InlineData("UNDEFINED", EventDirection.Indeterminate)]
    [InlineData("Underfrequency", EventDirection.Low)]
    [InlineData("Overfrequency", EventDirection.High)]
    public void DirectionFor_ClassifiesEventTypesCorrectly(string eventType, EventDirection expected)
    {
        Assert.Equal(expected, DomainConstants.DirectionFor(eventType));
    }

    [Theory]
    [InlineData("UNDEFINED", 2, EventCategory.Voltage)]
    [InlineData("UNDEFINED", 4, EventCategory.Voltage)]
    [InlineData("Undefined", 1, EventCategory.Frequency)]
    [InlineData("AMT", 2, EventCategory.Voltage)]
    [InlineData("Underfrequency", 1, EventCategory.Frequency)]
    public void CategoryFor_UsesSourceIdToSplitUndefinedEvents(string eventType, int sourceId, EventCategory expected)
    {
        Assert.Equal(expected, DomainConstants.CategoryFor(eventType, sourceId));
    }
}
