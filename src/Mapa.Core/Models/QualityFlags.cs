namespace Mapa.Core.Models;

/// <summary>
/// Bit layout per the MPleno data dictionary (Figura 5): bit0=inicio incerto,
/// bit1=fim incerto, bit2=frames invalidos, bit3=sem frames recebidos,
/// bit4=timeout, bits5-7=reservados.
/// </summary>
[Flags]
public enum QualityFlags
{
    None = 0,
    StartUncertain = 1 << 0,
    EndUncertain = 1 << 1,
    InvalidFrames = 1 << 2,
    NoFramesReceived = 1 << 3,
    Timeout = 1 << 4,
}

public static class QualityFlagsParser
{
    /// <summary>
    /// Parses an 8-character binary string (bit7..bit0, most significant bit first,
    /// matching the "quality" column format in Tabela 3) into <see cref="QualityFlags"/>.
    /// </summary>
    public static QualityFlags Parse(string quality)
    {
        if (string.IsNullOrWhiteSpace(quality))
            return QualityFlags.None;

        // String is written bit7..bit0 left-to-right; bit0 is the last character.
        var bits = quality.Trim();
        var value = Convert.ToInt32(bits, 2);
        return (QualityFlags)(value & 0b0001_1111);
    }
}
