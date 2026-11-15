using System.Collections.Concurrent;
using C64Bot.Libretro;

namespace C64Bot.App;

/// <summary>
/// What a PC key does in the game: press a key on the C64's keyboard, or a button on the joystick.
/// </summary>
public readonly record struct GameInput(RetroKey? Key = null, RetroPadButton? Button = null);

/// <summary>
/// Key presses and releases, passed from the window to the emulation thread.
/// </summary>
/// <remarks>
/// The core may only be used from the emulation thread, so the window doesn't press anything itself. It adds
/// presses and releases here, and the emulation loop applies them just before each frame.
/// </remarks>
public sealed class InputQueue
{
    private readonly ConcurrentQueue<(GameInput Input, bool Down)> _events = new();

    public void Add(GameInput input, bool down) => _events.Enqueue((input, down));

    /// <summary>
    /// Presses and releases everything added since the last call, in order. Call it on the emulation thread.
    /// </summary>
    public void ApplyTo(LibretroCore core)
    {
        while (_events.TryDequeue(out var e))
        {
            if (e.Input.Key is { } key)
            {
                if (e.Down)
                    core.PressKey(key);
                else
                    core.ReleaseKey(key);
            }

            if (e.Input.Button is { } button)
            {
                if (e.Down)
                    core.PressButton(button);
                else
                    core.ReleaseButton(button);
            }
        }
    }
}
