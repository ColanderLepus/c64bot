namespace C64Bot.Libretro;

/// <summary>
/// How a C64 joystick is held: any of its directions and its fire button, together.
/// </summary>
[Flags]
public enum Joystick
{
    None = 0,
    Up = 1,
    Down = 2,
    Left = 4,
    Right = 8,
    Fire = 16,
}
