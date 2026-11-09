namespace Mapa.Core.Models;

/// <summary>
/// Which side of nominal an event falls on: Low = afundamento/subtensão (sag) or subfrequência
/// (underfrequency); High = elevação/sobretensão (swell) or sobrefrequência (overfrequency).
/// Indeterminate covers VTCD's UNDEFINED event_type (start/end uncertain per quality bits).
/// </summary>
public enum EventDirection
{
    Low,
    High,
    Indeterminate,
}
