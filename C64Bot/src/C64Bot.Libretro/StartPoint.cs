namespace C64Bot.Libretro;

/// <summary>
/// A saved moment in a game that the game can be started from again and again, with or without changes to its RAM.
/// </summary>
/// <remarks>
/// The moment is kept as a save state: a copy of the whole emulated machine, held in memory. Which changes make sense
/// depends on the game and the moment, for example just before the game sets up a level.
/// </remarks>
public sealed class StartPoint
{
    private readonly LibretroCore _core;
    private readonly byte[] _state;

    private StartPoint(LibretroCore core, byte[] state)
    {
        _core = core;
        _state = state;
    }

    /// <summary>
    /// Saves the moment the core's game is at now.
    /// </summary>
    /// <exception cref="InvalidOperationException">No game has been loaded, or the core couldn't save its state.</exception>
    /// <exception cref="ObjectDisposedException">The core has been disposed.</exception>
    public static StartPoint Save(LibretroCore core)
    {
        ArgumentNullException.ThrowIfNull(core);
        return new StartPoint(core, core.SaveState());
    }

    /// <summary>
    /// Takes the game back to the saved moment, with no input (see <see cref="LibretroCore.LoadState"/>).
    /// </summary>
    /// <exception cref="InvalidOperationException">The core couldn't load the state.</exception>
    /// <exception cref="ObjectDisposedException">The core has been disposed.</exception>
    public void Start() => Start(RamPatch.Empty);

    /// <summary>
    /// Takes the game back to the saved moment, with no input, and makes the patch's changes to its RAM before it
    /// runs on.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Some of the patch's bytes don't fit in RAM. The game is back at the saved moment, with the changes before them.
    /// </exception>
    /// <exception cref="InvalidOperationException">The core couldn't load the state.</exception>
    /// <exception cref="ObjectDisposedException">The core has been disposed.</exception>
    public void Start(RamPatch patch)
    {
        ArgumentNullException.ThrowIfNull(patch);
        _core.LoadState(_state);
        patch.ApplyTo(_core);
    }
}
