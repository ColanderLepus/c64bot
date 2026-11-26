using C64Bot.Libretro;

namespace C64Bot.LodeRunner;

/// <summary>
/// A move the player can make: a step or a climb to the next tile, a dig, a wait until a hole closes, or a fall.
/// </summary>
/// <remarks>
/// <para>
/// Moves are what the planner builds its routes from. Each is worked out pass by pass with the game's own rules
/// (<see cref="GameState.Next"/>), so it's exact: how many passes it takes, where he ends up, the gold he takes on the
/// way, and the holes that open and close meanwhile.
/// </para>
/// <para>
/// A step or a climb ends in the middle of a tile, so a fall belongs to the move that led to it. If he's stopped
/// before that, it ends where he stopped. That can be below the middle of a tile: when a hole closes under him in a
/// fall, the brick stops him where he is. A move also ends when the level is completed. A move he wouldn't survive
/// isn't one.
/// </para>
/// <para>
/// When nothing holds him, as at the start of some levels, he falls whatever the joystick does, so the fall is his
/// only move.
/// </para>
/// </remarks>
public sealed class Move
{
    // Longer than any move takes: a fall from the top row to the bottom takes 80 passes, and a hole stays open 180.
    private const int MaxPasses = 250;

    private Move(MoveKind kind, IReadOnlyList<Joystick> passes, GameState end)
    {
        Kind = kind;
        Passes = passes;
        End = end;
    }

    public MoveKind Kind { get; }

    /// <summary>
    /// How the joystick is held in each pass of the move, one after another.
    /// </summary>
    public IReadOnlyList<Joystick> Passes { get; }

    /// <summary>
    /// The state after the move. Unless the level is completed, the joystick decides what he does in the next pass:
    /// he isn't falling or digging.
    /// </summary>
    public GameState End { get; }

    /// <summary>
    /// Works out every move the player can make from this state and survive.
    /// </summary>
    /// <exception cref="ArgumentException">He's digging, or the level has ended.</exception>
    /// <exception cref="NotSupportedException">The level has guards, which the rules don't have yet.</exception>
    public static IReadOnlyList<Move> AllFrom(GameState start)
    {
        if (start is not { Alive: true, Completed: false, Player.Digging: null })
            throw new ArgumentException("The level has ended, or he's digging.", nameof(start));

        if (!JoystickDecides(start))
            return Fall(start) is { } fall ? [fall] : [];

        Move?[] moves =
        [
            Go(start, MoveKind.Left, Joystick.Left),
            Go(start, MoveKind.Right, Joystick.Right),
            Go(start, MoveKind.Up, Joystick.Up),
            Go(start, MoveKind.Down, Joystick.Down),
            Dig(start, Direction.Left),
            Dig(start, Direction.Right),
            WaitForHole(start),
        ];

        return [.. moves.OfType<Move>()];
    }

    public override string ToString() =>
        $"{Kind}, {Passes.Count} passes, to {End.Player.Position.Column},{End.Player.Position.Row}";

    // Holds the joystick that way until he's in the middle of a tile he wasn't in the middle of, or stopped.
    private static Move? Go(GameState start, MoveKind kind, Joystick joystick)
    {
        var moving = new Moving(start);
        var before = start;
        while (true)
        {
            moving.Pass(joystick);
            var state = moving.State;
            if (!state.Alive || state.Completed)
                break;

            var stopped = IsInPlace(state, before);
            if (stopped && before == start)
                return null;

            if ((stopped || (IsInTheMiddle(state) && !IsInPlace(state, start))) && JoystickDecides(state))
                break;

            before = state;
        }

        return moving.ToMove(kind);
    }

    // Holds fire until the hole is open, after a step that way to turn him if he isn't facing it.
    private static Move? Dig(GameState start, Direction side)
    {
        var moving = new Moving(start);
        if (start.Player.DigSide != side)
        {
            moving.Pass(side == Direction.Left ? Joystick.Left : Joystick.Right);
            if (moving.State.Player.DigSide != side)
                return null;
        }

        var (column, row) = moving.State.Player.Position;
        var hole = new TilePosition(column + (side == Direction.Left ? -1 : 1), row + 1);
        if (moving.State.Holes.Any(open => open.Position == hole))
            return null;

        do
        {
            moving.Pass(Joystick.Fire);
        }
        while (moving.State is { Alive: true, Completed: false, Player.Digging: not null });

        // The dig stops at once if it isn't possible there, and the hole is there only if it went all the way.
        if (moving.State.Holes.All(open => open.Position != hole))
            return null;

        return moving.ToMove(side == Direction.Left ? MoveKind.DigLeft : MoveKind.DigRight);
    }

    private static Move? WaitForHole(GameState start)
    {
        if (start.Holes.Count == 0)
            return null;

        var moving = new Moving(start);
        var closes = start.Holes.Min(hole => hole.PassesLeft);
        while (moving.Passes.Count < closes)
            moving.Pass(Joystick.None);

        return moving.ToMove(MoveKind.WaitForHole);
    }

    private static Move? Fall(GameState start)
    {
        var moving = new Moving(start);
        do
        {
            moving.Pass(Joystick.None);
        }
        while (moving.State.Alive && !moving.State.Completed && !JoystickDecides(moving.State));

        return moving.ToMove(MoveKind.Fall);
    }

    // Whether the joystick decides what he does in the next pass: he won't fall or dig in it. He can still be falling
    // in the pass he lands, so that's looked at in the next.
    private static bool JoystickDecides(GameState state) =>
        state is { Alive: true, Completed: false, Player.Digging: null } && !state.Next(Joystick.None).Player.Falling;

    private static bool IsInTheMiddle(GameState state) => state.Player is { OffsetX: 0, OffsetY: 0 };

    private static bool IsInPlace(GameState state, GameState before) =>
        (state.Player.Position, state.Player.OffsetX, state.Player.OffsetY)
        == (before.Player.Position, before.Player.OffsetX, before.Player.OffsetY);

    // The passes of a move so far, and the state after them.
    private sealed class Moving(GameState start)
    {
        private readonly TilePosition _from = start.Player.Position;
        private readonly List<Joystick> _passes = [];

        public GameState State { get; private set; } = start;

        public IReadOnlyList<Joystick> Passes => _passes;

        public void Pass(Joystick joystick)
        {
            if (_passes.Count == MaxPasses)
                throw new InvalidOperationException($"A move from {_from} didn't end.");

            State = State.Next(joystick);
            _passes.Add(joystick);
        }

        // A move he doesn't survive isn't one.
        public Move? ToMove(MoveKind kind)
        {
            if (!State.Alive)
                return null;

            if (!State.Completed && !JoystickDecides(State))
                throw new InvalidOperationException($"A {kind} move from {_from} ended while he was falling.");

            return new Move(kind, _passes, State);
        }
    }
}
