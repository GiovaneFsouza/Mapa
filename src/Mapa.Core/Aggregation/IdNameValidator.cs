using Mapa.Core.Models;

namespace Mapa.Core.Aggregation;

public sealed record IdNameValidationResult(IReadOnlyList<string> UnmatchedIdNames)
{
    public bool HasMismatches => UnmatchedIdNames.Count > 0;
}

/// <summary>Detects idName values present in the Excel that don't exist in the selected XML.</summary>
public static class IdNameValidator
{
    public static IdNameValidationResult Validate(IEnumerable<PmuEvent> events, IReadOnlyList<Pmu> pmus)
    {
        var known = pmus.Select(p => p.IdName).ToHashSet(StringComparer.Ordinal);
        var unmatched = events
            .Select(e => e.IdName)
            .Distinct(StringComparer.Ordinal)
            .Where(idName => !known.Contains(idName))
            .OrderBy(idName => idName, StringComparer.Ordinal)
            .ToList();

        return new IdNameValidationResult(unmatched);
    }
}
