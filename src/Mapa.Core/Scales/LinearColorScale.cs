namespace Mapa.Core.Scales;

/// <summary>3-stop diverging gradient: blue (low) -> green (mid) -> red (high), linear RGB interpolation.</summary>
public sealed class LinearColorScale : IColorScale
{
    private static readonly (double T, byte R, byte G, byte B)[] Stops =
    {
        (0.0, 0x21, 0x66, 0xAC), // blue
        (0.5, 0x2E, 0xCC, 0x71), // green
        (1.0, 0xE7, 0x4C, 0x3C), // red
    };

    public string ToHexColor(double value, ScaleRange range)
    {
        var t = range.Normalize(value);

        var lower = Stops[0];
        var upper = Stops[^1];
        for (var i = 0; i < Stops.Length - 1; i++)
        {
            if (t >= Stops[i].T && t <= Stops[i + 1].T)
            {
                lower = Stops[i];
                upper = Stops[i + 1];
                break;
            }
        }

        var span = upper.T - lower.T;
        var localT = span <= 0 ? 0 : (t - lower.T) / span;

        var r = Lerp(lower.R, upper.R, localT);
        var g = Lerp(lower.G, upper.G, localT);
        var b = Lerp(lower.B, upper.B, localT);

        return $"#{r:X2}{g:X2}{b:X2}";
    }

    private static byte Lerp(byte a, byte b, double t) => (byte)Math.Round(a + ((b - a) * t));
}
