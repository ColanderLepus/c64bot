using System.Windows.Input;
using C64Bot.Libretro;

namespace C64Bot.App;

/// <summary>
/// Which PC key does what in the game: the arrow keys and Right Ctrl are the joystick, and most other keys are the
/// same keys on the C64's keyboard.
/// </summary>
/// <remarks>
/// The joystick keys go only to the joystick, so they can't clash with the core's own key map. The core decides
/// which C64 key each PC key is; for example, Left Ctrl is probably the C= key. The punctuation keys are named
/// after their places on a US keyboard, so on other layouts some may give a different character.
/// </remarks>
public static class KeyMap
{
    /// <summary>
    /// Finds what the key does in the game, or returns null if it does nothing there.
    /// </summary>
    public static GameInput? Find(Key key) => key switch
    {
        Key.Up => new(Button: RetroPadButton.Up),
        Key.Down => new(Button: RetroPadButton.Down),
        Key.Left => new(Button: RetroPadButton.Left),
        Key.Right => new(Button: RetroPadButton.Right),
        Key.RightCtrl => new(Button: RetroPadButton.B),

        >= Key.A and <= Key.Z => new(Key: RetroKey.A + (uint)(key - Key.A)),
        >= Key.D0 and <= Key.D9 => new(Key: RetroKey.D0 + (uint)(key - Key.D0)),
        >= Key.F1 and <= Key.F8 => new(Key: RetroKey.F1 + (uint)(key - Key.F1)),

        Key.Space => new(Key: RetroKey.Space),
        Key.Return => new(Key: RetroKey.Return),
        Key.Back => new(Key: RetroKey.Backspace),
        Key.Tab => new(Key: RetroKey.Tab),
        Key.Escape => new(Key: RetroKey.Escape),
        Key.Delete => new(Key: RetroKey.Delete),
        Key.Insert => new(Key: RetroKey.Insert),
        Key.Home => new(Key: RetroKey.Home),
        Key.LeftShift => new(Key: RetroKey.LeftShift),
        Key.RightShift => new(Key: RetroKey.RightShift),
        Key.LeftCtrl => new(Key: RetroKey.LeftCtrl),

        Key.OemQuotes => new(Key: RetroKey.Quote),
        Key.OemComma => new(Key: RetroKey.Comma),
        Key.OemMinus => new(Key: RetroKey.Minus),
        Key.OemPeriod => new(Key: RetroKey.Period),
        Key.OemQuestion => new(Key: RetroKey.Slash),
        Key.OemSemicolon => new(Key: RetroKey.Semicolon),
        Key.OemPlus => new(Key: RetroKey.Equals),
        Key.OemOpenBrackets => new(Key: RetroKey.LeftBracket),
        Key.OemPipe => new(Key: RetroKey.Backslash),
        Key.OemCloseBrackets => new(Key: RetroKey.RightBracket),
        Key.OemTilde => new(Key: RetroKey.Backquote),

        _ => null,
    };
}
