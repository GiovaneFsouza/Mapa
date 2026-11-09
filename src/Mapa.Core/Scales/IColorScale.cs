namespace Mapa.Core.Scales;

public interface IColorScale
{
    /// <summary>Returns a "#rrggbb" hex color for value within range.</summary>
    string ToHexColor(double value, ScaleRange range);
}
