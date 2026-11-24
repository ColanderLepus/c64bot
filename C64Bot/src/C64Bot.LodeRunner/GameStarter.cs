using C64Bot.Libretro;

namespace C64Bot.LodeRunner;

/// <summary>
/// Gets Lode Runner from a freshly loaded disk to the start of a new game once, and from then on starts a new game
/// at any level in about a second.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Start"/> runs the core flat out through loading, the trainer menu and the title screen into the demo,
/// presses fire, and saves the first frame of the new game. That frame falls in a short window: the game has set the
/// level to 1 and switched to play mode, but hasn't loaded the level from disk yet.
/// </para>
/// <para>
/// <see cref="StartLevel"/> goes back to that frame and puts a level where the game will look for it, so the game
/// sets that level up. Lives and score are those of a new game, unlike when levels are skipped with C=.
/// </para>
/// <para>
/// This is for development and tests, and the only code that writes the game's RAM (D13). The finished bot plays the
/// game as it is, loaded the regular way (D17).
/// </para>
/// </remarks>
public sealed class GameStarter
{
    // Loading takes 2,532 frames, the title screen, the demo's start and setting up its level well over 1,000 more,
    // and the new game starts a few frames after fire. The limits leave plenty of room.
    private const int MaxFramesToTheTrainerMenu = 3000;
    private const int MaxFramesToTheNextQuestion = 250;
    private const int MaxFramesToTheDemo = 3000;
    private const int MaxFramesToTheNewGame = 250;

    // The keyboard is read about once per frame, so a press has to last a few frames to be seen.
    private const int PressFrames = 5;

    private readonly LibretroCore _core;
    private readonly byte[] _newGame;

    private GameStarter(LibretroCore core, byte[] newGame)
    {
        _core = core;
        _newGame = newGame;
    }

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

        RunUntil("the trainer menu's question about unlimited lives", () => ScreenShows("UNLIMITED LIVES"),
            MaxFramesToTheTrainerMenu);
        Type(unlimitedLives ? RetroKey.Y : RetroKey.N);
        RunUntil("the question about invincibility", () => ScreenShows("INVINCIBILITY"),
            MaxFramesToTheNextQuestion);
        Type(RetroKey.N);
        RunUntil("the note about the level skip", () => ScreenShows("SKIPS"), MaxFramesToTheNextQuestion);
        Type(RetroKey.Space);

        // Fire is only seen once the demo runs, not while it sets up its level.
        var reader = new GamePhaseReader();
        RunUntil("the demo", () => reader.Update(core.SystemRam) == GamePhase.Demo && reader.MainLoopRunning,
            MaxFramesToTheDemo);

        // Fire ends the demo. The game can take some 20 frames to look at it, so it's held until the new game starts:
        // when the game switches to play mode, with the demo's player still dead; it brings him back to life just
        // before it loads the level, about 17 frames later. Fire, unlike a key, leaves nothing waiting to be read, so
        // the level will wait for the player's first move.
        core.PressButton(RetroPadButton.B);
        RunUntil("the new game", () => core.SystemRam[RamMap.GameMode] == 2, MaxFramesToTheNewGame);

        // Let go of fire and give the C64 a moment to see it, so the saved moment has no input, like every game
        // started from it (a load always starts with none, D15).
        core.ReleaseButton(RetroPadButton.B);
        core.RunFrames(2);

        if (core.SystemRam[RamMap.PlayerAlive] != 0)
            throw new InvalidOperationException("The new game was seen too late: its level is already being loaded.");

        return new GameStarter(core, core.SaveState());

        bool ScreenShows(string text) => TextScreen.Read(core.SystemRam).Contains(text);

        void RunUntil(string what, Func<bool> condition, int maxFrames)
        {
            for (var frame = 0; frame < maxFrames; frame++)
            {
                core.RunFrame();
                if (condition())
                    return;
            }

            throw new InvalidOperationException($"Lode Runner didn't get to {what} within {maxFrames} frames.");
        }

        void Type(RetroKey key)
        {
            core.PressKey(key);
            core.RunFrames(PressFrames);
            core.ReleaseKey(key);
            core.RunFrames(PressFrames);
        }
    }

    /// <summary>
    /// Starts a new game at this level. The game sets the level up in about 490 frames and waits for the player's
    /// first move.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Can be called any number of times; each call starts a new game from the moment <see cref="Start"/> saved. Games
    /// started with the same level repeat exactly when given the same joystick input.
    /// </para>
    /// <para>
    /// It loads a state, so it lets go of all input (<see cref="LibretroCore.LoadState"/>, D15): press the first move
    /// after calling it, even if the same move was held before. The phase is <see cref="GamePhase.LevelStarting"/>
    /// until that move, which has to be held until it's <see cref="GamePhase.Playing"/>: RAM doesn't show when the
    /// level is ready for it, and a short press can come too early.
    /// </para>
    /// <para>
    /// The level stays as given also after the player dies. The next one is loaded from the disk by the game.
    /// </para>
    /// </remarks>
    /// <param name="level">
    /// A level with the player in it: one from the disk, with changes or without, or a made-up one.
    /// </param>
    /// <exception cref="InvalidOperationException">The level has a tile the game can't store.</exception>
    public void StartLevel(Level level)
    {
        ArgumentNullException.ThrowIfNull(level);
        GoToStartOfNewGame();
        PutInGame(level);
    }

    private void GoToStartOfNewGame() => _core.LoadState(_newGame);

    // The game is about to set up a level. These are the level's tiles and number, where the game looks for them; it
    // has the number in three places, and counts from 0 in two of them.
    private void PutInGame(Level level)
    {
        _core.WriteSystemRam(RamMap.LevelBuffer, level.ToBytes());
        _core.WriteSystemRam(RamMap.LevelNumber, [(byte)level.Number]);
        _core.WriteSystemRam(RamMap.LevelNumberOnDisk, [(byte)(level.Number - 1)]);
        _core.WriteSystemRam(RamMap.LevelInBuffer, [(byte)(level.Number - 1)]);
    }
}
