namespace Mapa.Core.Aggregation;

/// <summary>
/// Event counts for one region (Pmu.Area), split by direction: Low = afundamento/subtensão or
/// subfrequência; High = elevação/sobretensão or sobrefrequência; Indeterminate = VTCD UNDEFINED.
/// </summary>
public sealed record RegionCount(string Region, int LowCount, int HighCount, int IndeterminateCount)
{
    public int Total => LowCount + HighCount + IndeterminateCount;
}
