namespace Mapa.Core.Models;

public static class DomainConstants
{
    public const double NominalVoltagePu = 1.0;
    public const double NominalFrequencyHz = 60.0;

    public static readonly IReadOnlySet<string> VoltageEventTypes = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        "IMT", "AMT", "EMT", "ITT", "ATT", "ETT", "UNDEFINED",
    };

    public static readonly IReadOnlySet<string> FrequencyEventTypes = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        "Underfrequency", "Overfrequency",
    };

    /// <summary>Afundamento/interrupção (below nominal): IMT, AMT, ITT, ATT.</summary>
    private static readonly IReadOnlySet<string> LowVoltageEventTypes = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase) { "IMT", "AMT", "ITT", "ATT" };

    /// <summary>Elevação (above nominal): EMT, ETT.</summary>
    private static readonly IReadOnlySet<string> HighVoltageEventTypes = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase) { "EMT", "ETT" };

    /// <summary>source_id of the system-frequency channel (2/3/4 are voltage phases A/B/C).</summary>
    public const int FrequencySourceId = 1;

    /// <summary>
    /// Both detectors write "UNDEFINED"/"Undefined" for events they cannot characterise, so the
    /// event type alone doesn't tell which grandeza it belongs to; the source_id does.
    /// </summary>
    public static EventCategory CategoryFor(string eventType, int sourceId) =>
        string.Equals(eventType, "UNDEFINED", StringComparison.OrdinalIgnoreCase)
            ? (sourceId == FrequencySourceId ? EventCategory.Frequency : EventCategory.Voltage)
            : CategoryFor(eventType);

    public static EventCategory CategoryFor(string eventType)
    {
        if (FrequencyEventTypes.Contains(eventType))
            return EventCategory.Frequency;
        if (VoltageEventTypes.Contains(eventType))
            return EventCategory.Voltage;

        throw new ArgumentOutOfRangeException(nameof(eventType),
            eventType, "event_type desconhecido: nao mapeia para VTCD nem Frequencia.");
    }

    public static double NominalFor(EventCategory category) => category switch
    {
        EventCategory.Voltage => NominalVoltagePu,
        EventCategory.Frequency => NominalFrequencyHz,
        _ => throw new ArgumentOutOfRangeException(nameof(category)),
    };

    /// <summary>
    /// Unit durations are expressed in, per grandeza. VTCD events last tens to hundreds of
    /// milliseconds and are stated in ms by the Procedimentos de Rede; frequency excursions run
    /// for seconds to minutes. Reporting both in seconds rendered a 42 ms sag as "0,0 s".
    /// </summary>
    public static string DurationUnitFor(EventCategory category) => category switch
    {
        EventCategory.Voltage => "ms",
        EventCategory.Frequency => "s",
        _ => throw new ArgumentOutOfRangeException(nameof(category)),
    };

    public static EventDirection DirectionFor(string eventType)
    {
        if (string.Equals(eventType, "Underfrequency", StringComparison.OrdinalIgnoreCase))
            return EventDirection.Low;
        if (string.Equals(eventType, "Overfrequency", StringComparison.OrdinalIgnoreCase))
            return EventDirection.High;
        if (LowVoltageEventTypes.Contains(eventType))
            return EventDirection.Low;
        if (HighVoltageEventTypes.Contains(eventType))
            return EventDirection.High;

        return EventDirection.Indeterminate; // UNDEFINED
    }
}
