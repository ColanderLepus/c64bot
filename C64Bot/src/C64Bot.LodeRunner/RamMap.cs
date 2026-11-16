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
    /// The level as played: one byte per tile (see <see cref="TileAddress"/>), with holes, the player and the guards
    /// in it. Its player and guard tiles can be halfway through a move (D14).
    /// </summary>
    public const int LevelAsPlayed = 0x0800;

    /// <summary>
    /// The level as built, laid out like <see cref="LevelAsPlayed"/>: the level's own tiles and gold, without holes,
    /// the player or the guards. Exit ladders join it once all gold is taken.
    /// </summary>
    public const int LevelAsBuilt = 0x0A00;

    /// <summary>
    /// The column of hidden exit ladder n is at this address + n, for n from 1 to <see cref="HiddenLadderCount"/>.
    /// </summary>
    public const int HiddenLadderColumns = 0x1200;

    /// <summary>
    /// The row of hidden exit ladder n is at this address + n, for n from 1 to <see cref="HiddenLadderCount"/>.
    /// </summary>
    public const int HiddenLadderRows = 0x1230;

    public const int HiddenLadderCount = 0x1301;

    /// <summary>The player's column, 0–27.</summary>
    public const int PlayerColumn = 0x03;

    /// <summary>The player's row, 0–15.</summary>
    public const int PlayerRow = 0x04;

    /// <summary>
    /// Where the player is within his tile, left to right: 0–4, 2 when he's centred on it.
    /// </summary>
    public const int PlayerStepX = 0x05;

    /// <summary>
    /// Where the player is within his tile, top to bottom: 0–4, 2 when he's centred on it.
    /// </summary>
    public const int PlayerStepY = 0x06;

    /// <summary>
    /// The player's sprite number, which shows his pose and facing: <c>$00</c>–<c>$02</c> run left, <c>$03</c>–<c>$05</c>
    /// rope left, <c>$06</c> dig left, <c>$07</c> fall left, <c>$08</c>–<c>$0F</c> the same facing right,
    /// <c>$10</c>–<c>$11</c> ladder.
    /// </summary>
    public const int PlayerSprite = 0x07;

    /// <summary><c>$00</c> while the player falls, <c>$20</c> when he doesn't, <c>$FF</c> when a level starts.</summary>
    public const int PlayerFalling = 0x1315;

    /// <summary><c>$00</c> when the player isn't digging, <c>$01</c> while he digs to his right, <c>$FF</c> to his left.</summary>
    public const int PlayerDigging = 0x1316;

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
    public const int LevelNumber = 0x1305;

    /// <summary>
    /// The level number as stored on disk, 0–149: one less than <see cref="LevelNumber"/>. When a level starts, the
    /// game loads this one from disk.
    /// </summary>
    public const int LevelNumberOnDisk = 0x1310;

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

    /// <summary>
    /// Gives the address of a tile in one of the level grids (<see cref="LevelAsBuilt"/> or
    /// <see cref="LevelAsPlayed"/>): 28 bytes per row, but rows 9–15 start 4 bytes later, so no row crosses a page.
    /// </summary>
    /// <param name="grid">The grid's first address.</param>
    /// <param name="column">0–27, from the left.</param>
    /// <param name="row">0–15, from the top.</param>
    public static int TileAddress(int grid, int column, int row) =>
        grid + Level.Width * row + (row >= 9 ? 4 : 0) + column;
}
