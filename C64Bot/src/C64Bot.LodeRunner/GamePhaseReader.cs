namespace C64Bot.LodeRunner;

/// <summary>
/// Works out Lode Runner's <see cref="GamePhase"/> from the C64's RAM, one frame at a time.
/// </summary>
/// <remarks>
/// <para>
/// The game does everything in a main loop. Each pass moves the player and the guards, then waits until the
/// interrupt counter (<see cref="RamMap.InterruptCounter"/>) reaches the game speed, and sets it back to 3. At normal
/// speed that's about 30 passes a second, against 50 frames. Nothing else sets the counter back; while the game is
/// busy elsewhere (setting up a level, playing a tune, waiting for a key) it only counts up.
/// </para>
/// <para>
/// So the reader takes the main loop to be running while the counter keeps dropping back to 3–5. That needs the
/// frames before, and so does telling a death during play from the start of a new game, so the reader keeps a
/// little history: give it every frame, in order.
/// </para>
/// </remarks>
public sealed class GamePhaseReader
{
    // A pass comes about every 1.7 frames, so a running main loop drops the counter at least every 2 or 3 frames.
    // A little more leaves room, at the cost of noticing a few frames late that the loop stopped.
    private const int MaxFramesBetweenPasses = 5;

    private int? _previousCounter;
    private int _framesSincePass = int.MaxValue;
    private GamePhase _phase = GamePhase.Other;

    /// <summary>
    /// The phase found by the last <see cref="Update"/>, <see cref="GamePhase.Other"/> before the first. Reading it
    /// doesn't count a frame.
    /// </summary>
    public GamePhase Phase => _phase;

    /// <summary>
    /// Whether the game's main loop was running at the last <see cref="Update"/>: the player and the guards move,
    /// in a real game or in the demo.
    /// </summary>
    public bool MainLoopRunning { get; private set; }

    /// <summary>
    /// Reads the phase from the RAM as it is after a frame. Call it exactly once per frame: each call counts as a
    /// frame, so a second call for the same frame looks like a frame without a pass of the main loop, and a few of
    /// those make it look stopped. To look at the phase again, use <see cref="Phase"/>.
    /// </summary>
    /// <param name="ram">The C64's 64 KB of RAM, for example <c>LibretroCore.SystemRam</c>.</param>
    public GamePhase Update(ReadOnlySpan<byte> ram)
    {
        MainLoopRunning = UpdateMainLoopRunning(ram[RamMap.InterruptCounter]);
        _phase = Read(ram, MainLoopRunning);
        return _phase;
    }

    private bool UpdateMainLoopRunning(int counter)
    {
        // A pass sets the counter back to 3, and up to two interrupts can follow before the frame ends. Counting
        // up past 255 wraps it to 0 or 1, which isn't a pass.
        var passed = _previousCounter is { } previous && counter < previous && counter is >= 3 and <= 5;
        _previousCounter = counter;

        if (passed)
            _framesSincePass = 0;
        else if (_framesSincePass < int.MaxValue)
            _framesSincePass++;

        return _framesSincePass < MaxFramesBetweenPasses;
    }

    private GamePhase Read(ReadOnlySpan<byte> ram, bool mainLoopRunning)
    {
        // Until the game's interrupt routine is in place, the game isn't running and its bytes mean nothing.
        var interruptRoutine = ram[RamMap.InterruptVector] | ram[RamMap.InterruptVector + 1] << 8;
        if (interruptRoutine != RamMap.GameInterruptRoutine)
            return GamePhase.Other;

        return ram[RamMap.GameMode] switch
        {
            0 => GamePhase.Title,
            1 => GamePhase.Demo,
            2 => ReadPlayPhase(ram, mainLoopRunning),
            _ => GamePhase.Other,
        };
    }

    private GamePhase ReadPlayPhase(ReadOnlySpan<byte> ram, bool mainLoopRunning)
    {
        if (ram[RamMap.PlayerAlive] != 0)
            return mainLoopRunning ? GamePhase.Playing : GamePhase.LevelStarting;

        if (ram[RamMap.Lives] == 0)
            return GamePhase.GameOver;

        // The demo ends by killing its player, and a new game gives him his lives before it brings him back to
        // life. So a dead player is only dying if he died during play.
        return _phase is GamePhase.Playing or GamePhase.Dying ? GamePhase.Dying : GamePhase.LevelStarting;
    }
}
