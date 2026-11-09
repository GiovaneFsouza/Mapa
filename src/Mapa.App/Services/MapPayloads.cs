using System.Text.Json.Serialization;

namespace Mapa.App.Services;

public sealed record MarkerEventPayload(
    [property: JsonPropertyName("eventId")] string EventId,
    [property: JsonPropertyName("eventType")] string EventType,
    [property: JsonPropertyName("date")] string Date,
    [property: JsonPropertyName("startTime")] string StartTime,
    [property: JsonPropertyName("endTime")] string EndTime,
    [property: JsonPropertyName("extremeValue")] double ExtremeValue,
    [property: JsonPropertyName("quality")] string Quality);

public sealed record MarkerPayload(
    [property: JsonPropertyName("idName")] string IdName,
    [property: JsonPropertyName("lat")] double Lat,
    [property: JsonPropertyName("lon")] double Lon,
    [property: JsonPropertyName("colorHex")] string ColorHex,
    [property: JsonPropertyName("radiusPx")] double RadiusPx,
    /// <summary>Higher = worse. Lets a cluster adopt its worst child's color without the JS
    /// needing to know each mode's notion of "worst" (see MetricSeverityRank).</summary>
    [property: JsonPropertyName("severityRank")] double SeverityRank,
    /// <summary>0..1 position on the active scale, used as the heat layer's weight.</summary>
    [property: JsonPropertyName("intensity")] double Intensity,
    [property: JsonPropertyName("events")] IReadOnlyList<MarkerEventPayload> Events,
    /// <summary>1-based order in which this PMU first detected the event (1 = earliest).</summary>
    [property: JsonPropertyName("detectionRank")] int DetectionRank = 0,
    /// <summary>Milliseconds after the first-detecting PMU that this PMU detected the event.</summary>
    [property: JsonPropertyName("detectionDelayMs")] double DetectionDelayMs = 0.0,
    /// <summary>Human-readable PMU/terminal name (config.json label); idName stays the join key.</summary>
    [property: JsonPropertyName("label")] string Label = "");

internal sealed record SetMarkersPayload([property: JsonPropertyName("markers")] IReadOnlyList<MarkerPayload> Markers);

internal sealed record SetHeatmapPayload([property: JsonPropertyName("enabled")] bool Enabled);

internal sealed record WebMessageEnvelope<T>(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("payload")] T Payload);

internal sealed record FitBoundsEnvelope([property: JsonPropertyName("type")] string Type);

internal sealed record IncomingWebMessage(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("idName")] string? IdName);
