using Mapa.Core.Models;

namespace Mapa.Core.Correlation;

public enum FrequencyEventDirection { Sub, Sobre, Misto }

public sealed class FrequencyParticipant
{
    public required Pmu Pmu { get; init; }
    public EventDirection Direction { get; init; }
    public double ExtremeHz { get; init; }
    public double DurationSeconds { get; init; }
}

public sealed class CorrelatedFrequencyEvent
{
    public required DateTime StartTime { get; init; }
    public required IReadOnlyList<FrequencyParticipant> Participants { get; init; }

    public IReadOnlyList<FrequencyParticipant> SubParticipants =>
        [.. Participants.Where(p => p.Direction == EventDirection.Low)];
    public IReadOnlyList<FrequencyParticipant> SobreParticipants =>
        [.. Participants.Where(p => p.Direction == EventDirection.High)];

    public FrequencyEventDirection EventType =>
        SubParticipants.Count > 0 && SobreParticipants.Count > 0 ? FrequencyEventDirection.Misto :
        SubParticipants.Count > 0 ? FrequencyEventDirection.Sub : FrequencyEventDirection.Sobre;

    public double? MedianSubHz   => Median(SubParticipants.Select(p => p.ExtremeHz));
    public double? MedianSobreHz => Median(SobreParticipants.Select(p => p.ExtremeHz));
    public double  MedianDurationSeconds => Median(Participants.Select(p => p.DurationSeconds)) ?? 0;

    private static double? Median(IEnumerable<double> values)
    {
        var list = values.OrderBy(v => v).ToList();
        if (list.Count == 0) return null;
        int mid = list.Count / 2;
        return list.Count % 2 == 1 ? list[mid] : (list[mid - 1] + list[mid]) / 2.0;
    }
}
