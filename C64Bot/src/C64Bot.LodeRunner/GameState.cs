using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace C64Bot.LodeRunner;

/// <summary>
/// Everything the bot knows about a level in play at one moment: the level, the player, the guards, the holes, and
/// the counters on the status line.
/// </summary>
/// <remarks>
/// Each part is read from where the game keeps it complete (D14, "Reading RAM" in <c>docs/lode-runner.md</c>). Most
/// frames end halfway through the game's update, but only rarely does that catch the player's move halfway; such a
/// frame is skipped (<see cref="TryRead"/> gives false).
/// </remarks>
public sealed class GameState
{
    private const byte PlayerTile = 9;
    private const byte AllGoldTaken = 0xFF;

    private GameState(
        Level level, Player player, IReadOnlyList<Guard> guards, IReadOnlyList<Hole> holes, ReadOnlySpan<byte> ram)
    {
        Level = level;
        Player = player;
        Guards = guards;
        Holes = holes;
        LevelNumber = ram[RamMap.LevelNumber];
        Lives = ram[RamMap.Lives];
        Score = ReadScore(ram);

        // The game sets the count to $FF once it has shown the exit ladders.
        var goldLeft = ram[RamMap.GoldLeft];
        GoldLeft = goldLeft == AllGoldTaken ? 0 : goldLeft;
    }

    public Level Level { get; }

    public Player Player { get; }

    /// <summary>
    /// The level's guards, in the game's order (numbers 1 to 5), including any waiting to respawn.
    /// </summary>
    public IReadOnlyList<Guard> Guards { get; }

    /// <summary>
    /// The holes the player has dug that are still open.
    /// </summary>
    public IReadOnlyList<Hole> Holes { get; }

    /// <summary>The level's number, 1–150.</summary>
    public int LevelNumber { get; }

    public int Lives { get; }

    public int Score { get; }

    /// <summary>
    /// The gold still to be taken, including gold that guards carry. 0 once it's all taken and the exit ladders are
    /// there.
    /// </summary>
    public int GoldLeft { get; }

    /// <summary>
    /// Reads the state from the C64's RAM, as it is after a frame.
    /// </summary>
    /// <param name="ram">The C64's 64 KB of RAM, for example <c>LibretroCore.SystemRam</c>.</param>
    /// <param name="state">The state, if this frame can be read.</param>
    /// <returns>
    /// False for a frame to skip: it caught the player's move halfway (the grid as played has no player at his
    /// column and row), or there's no level in play to read, as before a level is set up.
    /// </returns>
    public static bool TryRead(ReadOnlySpan<byte> ram, [NotNullWhen(true)] out GameState? state)
    {
        state = null;
        var player = Player.Read(ram);
        if (player is null)
            return false;

        var (column, row) = player.Position;
        if (ram[RamMap.TileAddress(RamMap.LevelAsPlayed, column, row)] != PlayerTile)
            return false;

        var guards = Guard.ReadAll(ram);
        var holes = Hole.ReadAll(ram);
        if (guards is null || holes is null)
            return false;

        state = new GameState(Level.Read(ram), player, guards, holes, ram);
        return true;
    }

    /// <summary>
    /// Draws the level as text, as <see cref="LodeRunner.Level.ToString"/> does, with what's on it: <c>o</c> for a
    /// hole, <c>G</c> for a guard, <c>g</c> for one waiting to respawn (where he'll appear), and <c>P</c> for the
    /// player. Each is drawn over the ones before, so a guard in a hole shows as <c>G</c>.
    /// </summary>
    public override string ToString()
    {
        var text = new StringBuilder(Level.ToString());
        foreach (var hole in Holes)
            Mark(hole.Position, 'o');
        foreach (var guard in Guards)
            Mark(guard.Position, guard.Respawning ? 'g' : 'G');
        Mark(Player.Position, 'P');
        return text.ToString();

        // Each line of the text is a row and a line break.
        void Mark(TilePosition position, char mark) => text[position.Row * (Level.Width + 1) + position.Column] = mark;
    }

    // Four bytes of BCD, lowest first: each byte holds two decimal digits.
    private static int ReadScore(ReadOnlySpan<byte> ram)
    {
        var score = 0;
        for (var i = 3; i >= 0; i--)
        {
            var b = ram[RamMap.Score + i];
            score = score * 100 + (b >> 4) * 10 + (b & 0x0F);
        }

        return score;
    }
}
