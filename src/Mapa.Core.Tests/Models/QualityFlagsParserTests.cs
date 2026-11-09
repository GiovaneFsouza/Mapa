using Mapa.Core.Models;

namespace Mapa.Core.Tests.Models;

public class QualityFlagsParserTests
{
    [Fact]
    public void Parse_AllZeros_IsNone()
    {
        Assert.Equal(QualityFlags.None, QualityFlagsParser.Parse("00000000"));
    }

    [Fact]
    public void Parse_Bit0_IsStartUncertain()
    {
        Assert.Equal(QualityFlags.StartUncertain, QualityFlagsParser.Parse("00000001"));
    }

    [Fact]
    public void Parse_Bit1_IsEndUncertain()
    {
        Assert.Equal(QualityFlags.EndUncertain, QualityFlagsParser.Parse("00000010"));
    }

    [Fact]
    public void Parse_Bit0AndBit1Set_CombinesFlags()
    {
        var flags = QualityFlagsParser.Parse("00000011");

        Assert.True(flags.HasFlag(QualityFlags.StartUncertain));
        Assert.True(flags.HasFlag(QualityFlags.EndUncertain));
    }

    [Fact]
    public void Parse_Bit4_IsTimeout()
    {
        Assert.Equal(QualityFlags.Timeout, QualityFlagsParser.Parse("00010000"));
    }
}
