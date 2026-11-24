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
    // The level.

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

    /// <summary>
    /// The level buffer: the level as stored, in <see cref="LevelData.ByteCount"/> bytes. The game sets the level up
    /// from here, and again after a death.
    /// </summary>
    public const int LevelBuffer = 0x1000;

    /// <summary>
    /// The level held in <see cref="LevelBuffer"/>, numbered as on disk. When a level is set up, the game reads it
    /// from disk only if this isn't <see cref="LevelNumberOnDisk"/>.
    /// </summary>
    public const int LevelInBuffer = 0x133B;

    // The player.

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

    /// <summary>
    /// 1 while the player is alive, 0 once he has died (caught, trapped in a hole, or the demo was ended).
    /// </summary>
    public const int PlayerAlive = 0x1314;

    /// <summary><c>$00</c> while the player falls, <c>$20</c> when he doesn't, <c>$FF</c> when a level starts.</summary>
    public const int PlayerFalling = 0x1315;

    /// <summary><c>$00</c> when the player isn't digging, <c>$01</c> while he digs to his right, <c>$FF</c> to his left.</summary>
    public const int PlayerDigging = 0x1316;

    // The guards.

    /// <summary>The number of guards in the level, 0–5. Their tables have an entry for each, numbered from 1.</summary>
    public const int GuardCount = 0x131C;

    /// <summary>The column of guard n is at this address + n, for n from 1 to <see cref="GuardCount"/>.</summary>
    public const int GuardColumns = 0x1260;

    /// <summary>The row of guard n is at this address + n.</summary>
    public const int GuardRows = 0x1268;

    /// <summary>
    /// Guard n's gold or hole timer is at this address + n: 0 when he's free, negative (<c>$80</c>–<c>$FF</c>) while
    /// he carries gold (counting up; he drops it at 0), positive while he's stuck in a hole (counting down), and
    /// <c>$7F</c> while he waits to respawn.
    /// </summary>
    public const int GuardTimers = 0x1270;

    /// <summary>Where guard n is within his tile, left to right, is at this address + n: 0–4, 2 when centred.</summary>
    public const int GuardStepsX = 0x1278;

    /// <summary>Where guard n is within his tile, top to bottom, is at this address + n: 0–4, 2 when centred.</summary>
    public const int GuardStepsY = 0x1280;

    /// <summary>
    /// Guard n's sprite number is at this address + n: <c>$00</c>–<c>$02</c> run left, <c>$03</c>–<c>$05</c> rope
    /// left, <c>$06</c> fall left, <c>$07</c>–<c>$0D</c> the same facing right, <c>$0E</c>–<c>$0F</c> ladder.
    /// </summary>
    public const int GuardSprites = 0x1288;

    /// <summary>
    /// Guard n's respawn timer is at this address + n: 0 while he's in play; after he dies it starts at 20 and counts
    /// down once per pass, and he appears at his respawn place when it reaches 0.
    /// </summary>
    public const int GuardRespawnTimers = 0x1298;

    // The holes.

    /// <summary>
    /// The column of the hole in slot i is at this address + i, for slots 0 to <see cref="Hole.MaxCount"/> − 1. Only
    /// slots whose timer (<see cref="HoleTimers"/>) isn't 0 hold a hole.
    /// </summary>
    public const int HoleColumns = 0x12A0;

    /// <summary>The row of the hole in slot i is at this address + i.</summary>
    public const int HoleRows = 0x12C0;

    /// <summary>
    /// The timer of the hole in slot i is at this address + i: 0 for a free slot; 180 when a dig finishes, then one
    /// less each pass. The hole fills in, in pictures, at 20 and 10, and is a brick again at 0.
    /// </summary>
    public const int HoleTimers = 0x12E0;

    // The game.

    /// <summary>
    /// The game's own mode: 0 title screen, 1 demo, 2 play (also while the game-over sign and the high-score list
    /// show), 3 play from the level editor, 5 level editor.
    /// </summary>
    public const int GameMode = 0x1306;

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

    public const int Lives = 0x1312;

    /// <summary>
    /// The score, 4 bytes of BCD (two decimal digits per byte), lowest byte first.
    /// </summary>
    public const int Score = 0x130A;

    /// <summary>
    /// The gold still to be taken in the level; 0, or <c>$FF</c>, once it's all taken.
    /// </summary>
    public const int GoldLeft = 0x130E;

    // The game running.

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
