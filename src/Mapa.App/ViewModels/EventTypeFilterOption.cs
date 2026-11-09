using Mapa.Core.Models;

namespace Mapa.App.ViewModels;

public enum EventTypeFilterOption
{
    Vtcd,
    Frequencia,
    Ambos,
}

public static class EventTypeFilterOptionExtensions
{
    public static EventCategory? ToCategory(this EventTypeFilterOption option) => option switch
    {
        EventTypeFilterOption.Vtcd => EventCategory.Voltage,
        EventTypeFilterOption.Frequencia => EventCategory.Frequency,
        EventTypeFilterOption.Ambos => null,
        _ => throw new ArgumentOutOfRangeException(nameof(option)),
    };
}
