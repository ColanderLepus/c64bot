namespace C64Bot.Libretro;

/// <summary>
/// A button on the RetroPad, libretro's standard gamepad, numbered as in <c>libretro.h</c>
/// (<c>RETRO_DEVICE_ID_JOYPAD_*</c>).
/// </summary>
/// <remarks>
/// Each core decides what the buttons do. The VICE core turns the D-pad into the C64 joystick's directions
/// and <see cref="B"/> into its fire button, on the port chosen by the core option <c>vice_joyport</c>. Only
/// the buttons this project uses are listed.
/// </remarks>
public enum RetroPadButton : uint
{
    /// <summary>
    /// The fire button, in the VICE core.
    /// </summary>
    B = 0,

    Up = 4,
    Down = 5,
    Left = 6,
    Right = 7,
}
