using C64Bot.Libretro;

namespace C64Bot.App;

/// <summary>
/// What a PC key does in the game: press a key on the C64's keyboard, or a button on the joystick.
/// </summary>
public readonly record struct GameInput(RetroKey? Key = null, RetroPadButton? Button = null);
