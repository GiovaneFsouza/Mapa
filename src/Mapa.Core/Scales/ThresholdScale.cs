namespace Mapa.Core.Scales;

/// <summary>
/// Two-boundary threshold scale where L1 is always the lower value and L2 the higher value.
///
/// Normal (Sobre / Quantity / Duration):  value &lt; L1 → green, L1 ≤ value &lt; L2 → yellow, value ≥ L2 → red.
/// Inverted (Sub direction):              value &lt; L1 → red,   L1 ≤ value &lt; L2 → yellow, value ≥ L2 → green.
///
/// Setting IsInverted = true flips the color semantics so that lower values are shown as worse.
/// </summary>
public sealed record ThresholdScale(double L1, double L2, bool IsInverted = false)
{
    public const string Green  = "#2ECC71";
    public const string Yellow = "#F39C12";
    public const string Red    = "#E74C3C";

    public string ColorFor(double value) =>
        IsInverted
            ? (value < L1 ? Red : value < L2 ? Yellow : Green)
            : (value < L1 ? Green : value < L2 ? Yellow : Red);

    public double IntensityFor(double value)
    {
        if (IsInverted)
        {
            var range = L2 - L1;
            return range <= 0 ? 0.5 : Math.Min(1.0, Math.Max(0, (L2 - value) / range));
        }
        return L2 <= 0 ? 0.5 : Math.Min(1.0, Math.Max(0, value / L2));
    }
}
