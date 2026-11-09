using Mapa.Core.Scales;

namespace Mapa.Core.Tests.Scales;

public class LinearColorScaleTests
{
    private readonly LinearColorScale _scale = new();
    private readonly ScaleRange _range = new(0, 1.2);

    [Fact]
    public void ToHexColor_AtMin_IsBlue()
    {
        Assert.Equal("#2166AC", _scale.ToHexColor(0.0, _range));
    }

    [Fact]
    public void ToHexColor_AtMidpoint_IsGreen()
    {
        Assert.Equal("#2ECC71", _scale.ToHexColor(0.6, _range));
    }

    [Fact]
    public void ToHexColor_AtMax_IsRed()
    {
        Assert.Equal("#E74C3C", _scale.ToHexColor(1.2, _range));
    }

    [Fact]
    public void ToHexColor_OutOfRangeValues_ClampToEndpointColors()
    {
        Assert.Equal(_scale.ToHexColor(0.0, _range), _scale.ToHexColor(-10, _range));
        Assert.Equal(_scale.ToHexColor(1.2, _range), _scale.ToHexColor(99, _range));
    }
}
