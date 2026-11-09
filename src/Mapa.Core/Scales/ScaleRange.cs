namespace Mapa.Core.Scales;

public sealed record ScaleRange(double Min, double Max)
{
    /// <summary>Normalizes value into [0,1], clamped. Returns 0.5 for a degenerate (Min==Max) range.</summary>
    public double Normalize(double value)
    {
        if (Max <= Min)
            return 0.5;

        var t = (value - Min) / (Max - Min);
        return Math.Clamp(t, 0.0, 1.0);
    }

    /// <summary>Computes the tightest range covering the given values, falling back to [0,1] when empty.</summary>
    public static ScaleRange From(IReadOnlyCollection<double> values)
    {
        if (values.Count == 0)
            return new ScaleRange(0, 1);

        var min = values.Min();
        var max = values.Max();
        return min == max ? new ScaleRange(min, min + 1) : new ScaleRange(min, max);
    }
}
