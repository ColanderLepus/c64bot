using C64Bot.Libretro;

namespace C64Bot.LodeRunner;

/// <summary>
/// Gets Lode Runner from a freshly loaded disk to the start of a new game once, and from then on starts a new game
/// at any level in about a second.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Start"/> runs the core flat out through loading, the trainer menu and the title screen into the demo,
/// presses SPACE, and saves a state at the first frame of the new game. That frame falls in a short window: the game
/// has set the level to 1 and switched to play mode, but hasn't loaded the level from disk yet.
/// </para>
/// <para>
/// <see cref="StartLevel"/> loads that state and writes the wanted level number into RAM, so the game loads that
/// level itself. Lives and score are those of a new game, unlike when levels are skipped with C=. This is the only
/// place the bot writes the game's RAM (D13).
/// </para>
/// </remarks>
public sealed class GameStarter
{
    /// <summary>The highest level number; the game has 150 levels.</summary>
    public const int MaxLevel = 150;

    // Loading takes 2,532 frames, the title screen, the demo's start and setting up its level well over 1,000 more,
    // and the new game starts a few frames after SPACE. The limits leave plenty of room.
    private const int MaxFramesToTheTrainerMenu = 3000;
    private const int MaxFramesToTheNextQuestion = 250;
    private const int MaxFramesToTheDemo = 3000;
    private const int MaxFramesToTheNewGame = 250;

    // The keyboard is scanned about once per frame, so a press has to last a few frames to be seen.
    private const int KeyPressFrames = 5;

    private readonly LibretroCore _core;
    private readonly byte[] _newGame;

    private GameStarter(LibretroCore core, byte[] newGame, int frames)
    {
        _core = core;
        _newGame = newGame;
        StartFrames = frames;
    }

    /// <summary>
    /// The number of frames <see cref="Start"/> ran.
    /// </summary>
    public int StartFrames { get; }

    /// <summary>
    /// Runs the core flat out from a freshly loaded disk to the start of a new game, and remembers that moment.
    /// </summary>
    /// <param name="core">The core, with Lode Runner's disk loaded and not run yet.</param>
    /// <param name="unlimitedLives">
    /// The answer to the trainer's question about unlimited lives. Invincibility is always answered with no (D6).
    /// </param>
    /// <exception cref="InvalidOperationException">The game didn't get where it should within the expected time.</exception>
    public static GameStarter Start(LibretroCore core, bool unlimitedLives)
    {
        ArgumentNullException.ThrowIfNull(core);
        var frames = 0;

        RunUntil("the trainer menu's question about unlimited lives", () => ScreenText().Contains("UNLIMITED LIVES"),
            MaxFramesToTheTrainerMenu);
        Type(unlimitedLives ? RetroKey.Y : RetroKey.N);
        RunUntil("the question about invincibility", () => ScreenText().Contains("INVINCIBILITY"),
            MaxFramesToTheNextQuestion);
        Type(RetroKey.N);
        RunUntil("the note about the level skip", () => ScreenText().Contains("SKIPS"), MaxFramesToTheNextQuestion);
        Type(RetroKey.Space);

        // SPACE is only seen once the demo runs, not while it sets up its level.
        var reader = new GamePhaseReader();
        RunUntil("the demo", () => reader.Update(core.SystemRam) == GamePhase.Demo && reader.MainLoopRunning,
            MaxFramesToTheDemo);

        // SPACE ends the demo. The new game starts when the game switches to play mode, with the demo's player still
        // dead; it brings him back to life just before it loads the level, about 17 frames later.
        core.PressKey(RetroKey.Space);
        var spaceDown = true;
        var pressedFrames = 0;
        RunUntil("the new game", () =>
        {
            if (spaceDown && ++pressedFrames == KeyPressFrames)
            {
                core.ReleaseKey(RetroKey.Space);
                spaceDown = false;
            }

            return core.SystemRam[RamMap.GameMode] == 2;
        }, MaxFramesToTheNewGame);

        // The C64's keyboard is part of the state, so SPACE must be up there before saving, or it would stay held
        // down in every game started from it. A key reaches the C64 up to a frame late.
        if (spaceDown)
            core.ReleaseKey(RetroKey.Space);
        RunFrames(2);

        if (core.SystemRam[RamMap.Alive] != 0)
            throw new InvalidOperationException("The new game was seen too late: its level is already being loaded.");

        return new GameStarter(core, core.SaveState(), frames);

        void RunUntil(string what, Func<bool> condition, int maxFrames)
        {
            for (var frame = 0; frame < maxFrames; frame++)
            {
                core.RunFrame();
                frames++;
                if (condition())
                    return;
            }

            throw new InvalidOperationException($"Lode Runner didn't get to {what} within {maxFrames} frames.");
        }

        void Type(RetroKey key)
        {
            core.PressKey(key);
            RunFrames(KeyPressFrames);
            core.ReleaseKey(key);
            RunFrames(KeyPressFrames);
        }

        void RunFrames(int count)
        {
            for (var i = 0; i < count; i++)
            {
                core.RunFrame();
                frames++;
            }
        }

        // The C64's 40×25 text screen at $0400 as one string, row after row. Screen codes 1-26 are A-Z and 32-63
        // match ASCII; bit 7 only means the character is shown reversed.
        string ScreenText()
        {
            var screen = core.SystemRam[0x0400..0x07E8];
            var text = new char[screen.Length];
            for (var i = 0; i < screen.Length; i++)
            {
                var code = screen[i] & 0x7F;
                text[i] = code switch { < 32 => (char)('@' + code), < 64 => (char)code, _ => '#' };
            }

            return new string(text);
        }
    }

    /// <summary>
    /// Starts a new game at the given level. The game then loads and sets up the level by itself, and starts it
    /// about 640 frames later (<see cref="GamePhase.Playing"/>), without waiting for a move.
    /// </summary>
    /// <remarks>
    /// Can be called any number of times; each call starts a new game from the moment <see cref="Start"/> saved.
    /// The level doesn't wait for a first move because the SPACE that started the new game is still waiting to be
    /// read, as in any new game. After a death, the level does wait for a move.
    /// </remarks>
    /// <param name="level">The level, 1 to <see cref="MaxLevel"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">The level is outside 1 to <see cref="MaxLevel"/>.</exception>
    public void StartLevel(int level)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(level, MaxLevel);

        _core.LoadState(_newGame);
        _core.WriteSystemRam(RamMap.LevelNumberOnDisk, [(byte)(level - 1)]);
        _core.WriteSystemRam(RamMap.LevelNumber, [(byte)level]);
    }
}
