using Mapa.Core.Models;

namespace Mapa.Core.Filtering;

public static class EventFilterService
{
    /// <summary>
    /// Keeps events whose start falls within [criteria.Start, criteria.End] (inclusive) and,
    /// when CategoryFilter is set, whose category matches.
    /// </summary>
    public static IReadOnlyList<PmuEvent> Filter(IEnumerable<PmuEvent> events, FilterCriteria criteria)
    {
        IEnumerable<PmuEvent> query = events.Where(e =>
            e.StartTimestamp >= criteria.Start && e.StartTimestamp <= criteria.End);

        if (criteria.CategoryFilter is { } category)
            query = query.Where(e => e.Category == category);

        return query.ToList();
    }
}
