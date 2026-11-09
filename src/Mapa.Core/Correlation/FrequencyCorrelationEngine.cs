using Mapa.Core.Models;

namespace Mapa.Core.Correlation;

/// <summary>
/// Groups frequency events from multiple PMUs into correlated global disturbances using a
/// greedy time-gap algorithm: events whose start time falls within <paramref name="correlationWindow"/>
/// of the group's earliest start time are merged into one group. Groups with fewer than
/// <paramref name="minimumPmus"/> distinct PMUs are discarded as likely noise.
/// When a PMU reports multiple events in the same window the one with the largest absolute
/// deviation from the nominal frequency is kept (most informative).
/// </summary>
public sealed class FrequencyCorrelationEngine
{
    public IReadOnlyList<CorrelatedFrequencyEvent> Correlate(
        IEnumerable<PmuEvent> allEvents,
        IReadOnlyList<Pmu> allPmus,
        TimeSpan correlationWindow,
        int minimumPmus)
    {
        var pmusByIdName = allPmus
            .GroupBy(p => p.IdName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var nominal = DomainConstants.NominalFor(EventCategory.Frequency);

        var freqEvents = allEvents
            .Where(e => e.Category == EventCategory.Frequency)
            .OrderBy(e => e.StartTimestamp)
            .ToList();

        if (freqEvents.Count == 0) return [];

        // Greedy grouping
        var rawGroups = new List<List<PmuEvent>>();
        var current = new List<PmuEvent> { freqEvents[0] };

        for (int i = 1; i < freqEvents.Count; i++)
        {
            var e = freqEvents[i];
            if (e.StartTimestamp - current[0].StartTimestamp <= correlationWindow)
                current.Add(e);
            else
            {
                rawGroups.Add(current);
                current = [e];
            }
        }
        rawGroups.Add(current);

        var result = new List<CorrelatedFrequencyEvent>();
        foreach (var group in rawGroups)
        {
            // One representative event per PMU idName: largest deviation from nominal
            var byIdName = group
                .GroupBy(e => e.IdName, StringComparer.Ordinal)
                .Select(g => g.MaxBy(e => Math.Abs(e.ExtremeValue - nominal))!)
                .ToList();

            var participants = new List<FrequencyParticipant>();
            foreach (var ev in byIdName)
            {
                if (!pmusByIdName.TryGetValue(ev.IdName, out var matchingPmus)) continue;
                foreach (var pmu in matchingPmus)
                    participants.Add(new FrequencyParticipant
                    {
                        Pmu             = pmu,
                        Direction       = ev.Direction,
                        ExtremeHz       = ev.ExtremeValue,
                        DurationSeconds = ev.Duration.TotalSeconds,
                    });
            }

            if (participants.Count < minimumPmus) continue;

            result.Add(new CorrelatedFrequencyEvent
            {
                StartTime    = group[0].StartTimestamp,
                Participants = participants,
            });
        }

        return [.. result.OrderByDescending(e => e.StartTime)];
    }
}
