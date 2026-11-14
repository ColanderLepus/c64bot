namespace C64Bot.Libretro;

/// <summary>
/// A key on the host's keyboard, numbered as libretro numbers it (<c>RETROK_*</c> in <c>libretro.h</c>).
/// </summary>
/// <remarks>
/// The core turns these into presses on the emulated machine's keyboard. Only the keys this project uses are
/// listed. Letters are numbered like lowercase ASCII.
/// </remarks>
public enum RetroKey : uint
{
    Return = 13,
    Space = 32,
    N = 110,
    Y = 121,
}
