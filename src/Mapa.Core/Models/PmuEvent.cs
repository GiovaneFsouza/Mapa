namespace Mapa.Core.Models;

/// <summary>One row of the events Excel (Tabela 3 shape, idName instead of pmu_id).</summary>
public sealed record PmuEvent(
    string EventId,
    string IdName,
    int SourceId,
    string EventType,
    EventCategory Category,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    double ExtremeValue,
    QualityFlags Quality,
    string QualityRaw)
{
    public DateTime StartTimestamp => Date.ToDateTime(StartTime);

    public DateTime EndTimestamp => Date.ToDateTime(EndTime);

    public TimeSpan Duration => EndTimestamp - StartTimestamp;

    public EventDirection Direction => DomainConstants.DirectionFor(EventType);

    /// <summary>True when every bit of the raw quality field is 0 (including reserved bits 5-7).</summary>
    public bool IsQualityOk => QualityRaw.Trim().All(c => c == '0');

    public static PmuEvent Create(
        string eventId, string idName, int sourceId, string eventType,
        DateOnly date, TimeOnly startTime, TimeOnly endTime,
        double extremeValue, string qualityRaw)
    {
        var category = DomainConstants.CategoryFor(eventType, sourceId);
        var quality = QualityFlagsParser.Parse(qualityRaw);
        return new PmuEvent(eventId, idName, sourceId, eventType, category,
            date, startTime, endTime, extremeValue, quality, qualityRaw);
    }
}
