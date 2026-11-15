namespace C64Bot.Libretro;

/// <summary>
/// A run of consecutive RAM addresses whose bytes differ between two <see cref="RamImage"/>s.
/// </summary>
/// <param name="Start">The first address of the run.</param>
/// <param name="Before">The bytes in the earlier image, one per address.</param>
/// <param name="After">The bytes in the later image, one per address.</param>
public sealed record RamChange(int Start, byte[] Before, byte[] After)
{
    /// <summary>
    /// The address just after the run, so the run covers <c>Start..End</c>.
    /// </summary>
    public int End => Start + Before.Length;
}
