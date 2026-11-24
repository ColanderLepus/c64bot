namespace C64Bot.Libretro;

/// <summary>
/// The C64's text screen: 25 rows of 40 characters, which the machine keeps in RAM as codes of its own.
/// </summary>
public static class TextScreen
{
    private const int FirstAddress = 0x0400;
    private const int Length = 40 * 25;

    /// <summary>
    /// Reads the text on the screen as one string, row after row. Letters come out as A–Z, graphics characters as
    /// <c>#</c>.
    /// </summary>
    /// <param name="ram">The C64's 64 KB of RAM, for example <see cref="LibretroCore.SystemRam"/>.</param>
    public static string Read(ReadOnlySpan<byte> ram)
    {
        var text = new char[Length];
        for (var i = 0; i < Length; i++)
        {
            // Codes 1-26 are A-Z and 32-63 match ASCII; bit 7 only means the character is shown reversed.
            var code = ram[FirstAddress + i] & 0x7F;
            text[i] = code switch { < 32 => (char)('@' + code), < 64 => (char)code, _ => '#' };
        }

        return new string(text);
    }
}
