namespace C64Bot.LodeRunner;

/// <summary>
/// The addresses in the C64's RAM where Lode Runner (version A on our disk) keeps the data the bot reads.
/// </summary>
/// <remarks>
/// The addresses come from a disassembly of the original game, and the meanings and values are described in the
/// RAM map in <c>docs/lode-runner.md</c>, with how each one was confirmed.
/// </remarks>
public static class RamMap
{
    /// <summary>
    /// The C64's interrupt vector (2 bytes, low byte first): the routine run on every interrupt. It points to
    /// <see cref="GameInterruptRoutine"/> once the game runs.
    /// </summary>
    public const int InterruptVector = 0x0314;

    /// <summary>
    /// Lode Runner's interrupt routine, which plays sound and reads the keyboard.
    /// </summary>
    public const int GameInterruptRoutine = 0x648A;

    /// <summary>
    /// The level number, 1–150. It goes up as soon as a level is completed (or skipped), before the next one is
    /// set up.
    /// </summary>
    public const int Level = 0x1305;

    /// <summary>
    /// The level number as stored on disk, 0–149: one less than <see cref="Level"/>. When a level starts, the
    /// game loads this one from disk.
    /// </summary>
    public const int LevelOnDisk = 0x1310;

    /// <summary>
    /// The game's own mode: 0 title screen, 1 demo, 2 play (also while the game-over sign and the high-score list
    /// show), 3 play from the level editor, 5 level editor.
    /// </summary>
    public const int GameMode = 0x1306;

    /// <summary>
    /// 1 while the player is alive, 0 once he has died (caught, trapped in a hole, or the demo was ended).
    /// </summary>
    public const int Alive = 0x1314;

    /// <summary>
    /// The gold still to be taken in the level; 0, or <c>$FF</c>, once it's all taken.
    /// </summary>
    public const int GoldLeft = 0x130E;

    public const int Lives = 0x1312;

    /// <summary>
    /// Counted up by every interrupt (60 a second) and set back to 3 by each pass of the game's main loop, which
    /// waits until it reaches the game speed (5 at normal speed).
    /// </summary>
    public const int InterruptCounter = 0x132D;
}
