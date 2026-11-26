using System.Diagnostics.CodeAnalysis;
using System.Text;
using C64Bot.Libretro;

namespace C64Bot.LodeRunner;

/// <summary>
/// Everything the bot knows about a level in play at one moment: the level, the player, the guards, the holes, and
/// the counters on the status line.
/// </summary>
/// <remarks>
/// The game moves everything in passes of its main loop, about 30 a second: first the player, then the holes, then
/// the guards. A frame can end halfway through a pass. If that's before the holes are done, what the RAM says doesn't
/// fit together, and the frame is skipped (<see cref="TryRead"/> gives false). So the player, the holes, the gold and
/// the exit ladders are always as they are after a whole pass. A guard still being moved is one move behind (D14,
/// "Reading RAM" in <c>docs/lode-runner.md</c>).
/// </remarks>
public sealed class GameState
{
    private const byte AllGoldTaken = 0xFF;

    internal GameState(
        Level level,
        Player player,
        IReadOnlyList<Guard> guards,
        IReadOnlyList<Hole> holes,
        int lives,
        int score,
        int goldLeft,
        bool alive,
        bool completed)
    {
        Level = level;
        Player = player;
        Guards = guards;
        Holes = holes;
        Lives = lives;
        Score = score;
        GoldLeft = goldLeft;
        Alive = alive;
        Completed = completed;
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

    public int Lives { get; }

    public int Score { get; }

    /// <summary>
    /// The gold still to be taken, including gold that guards carry. 0 once it's all taken and the exit ladders are
    /// there.
    /// </summary>
    public int GoldLeft { get; }

    /// <summary>
    /// False once the player is dead: a hole closed on him, or a guard caught him.
    /// </summary>
    public bool Alive { get; }

    /// <summary>
    /// True once the player has completed the level: on the top row, with no gold left.
    /// </summary>
    public bool Completed { get; }

    /// <summary>
    /// Reads the state from the C64's RAM, as it is after a frame.
    /// </summary>
    /// <param name="ram">The C64's 64 KB of RAM, for example <c>LibretroCore.SystemRam</c>.</param>
    /// <param name="state">The state, if this frame can be read.</param>
    /// <returns>
    /// False for a frame to skip: it ended while the game was moving the player or closing holes, or setting up a
    /// level, or there's no level in play to read.
    /// </returns>
    public static bool TryRead(ReadOnlySpan<byte> ram, [NotNullWhen(true)] out GameState? state)
    {
        state = null;
        if (!IsWhole(ram))
            return false;

        var player = Player.Read(ram);
        var guards = Guard.ReadAll(ram);
        var holes = Hole.ReadAll(ram);
        if (player is null || guards is null || holes is null)
            return false;

        // The game sets the count to $FF once it has shown the exit ladders.
        var goldLeft = ram[RamMap.GoldLeft];

        // Completing a level moves the game on to the next number, while it still holds this level.
        var completed = ram[RamMap.LevelNumberOnDisk] != ram[RamMap.LevelInBuffer];

        state = new GameState(
            Level.Read(ram),
            player,
            guards,
            holes,
            ram[RamMap.Lives],
            ReadScore(ram),
            goldLeft == AllGoldTaken ? 0 : goldLeft,
            ram[RamMap.PlayerAlive] != 0,
            completed);
        return true;
    }

    /// <summary>
    /// Works out the state after the game's next pass, if the joystick is held this way.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The game moves everything in passes, about 30 a second, and looks at the joystick once in each. It's worked out
    /// by the game's own rules (D18): the player's moves and falls, digging, holes closing, gold, the exit ladders,
    /// and how the level ends. This state stays as it was.
    /// </para>
    /// <para>
    /// Once the player is dead or the level is completed, nothing moves any more, and it gives this state. What the
    /// game does then (taking a life or giving one, adding the level's points, starting the next level) isn't in it.
    /// </para>
    /// </remarks>
    /// <param name="joystick">How the joystick is held when the game looks at it.</param>
    /// <exception cref="NotSupportedException">The level has guards, which the rules don't have yet.</exception>
    public GameState Next(Joystick joystick)
    {
        if (Guards.Count > 0)
            throw new NotSupportedException("The rules don't have guards yet.");

        return Alive && !Completed ? Pass.Run(this, joystick) : this;
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

    // Whether the level is set up, and the main loop isn't between the start of a pass and its last part, the guards.
    private static bool IsWhole(ReadOnlySpan<byte> ram)
    {
        var position = ram[RamMap.MainCodePosition] | ram[RamMap.MainCodePosition + 1] << 8;
        return position is >= RamMap.LevelSetUp and not (> RamMap.MainLoop and <= RamMap.MainLoopGuards);
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
