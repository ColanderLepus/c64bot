using C64Bot.Libretro;

namespace C64Bot.LodeRunner.Tests;

// Races GameState.Next against the running game: every state read while playing must be exactly what Next gives for the
// passes since the one before. Counts the situations it comes through.
internal sealed class Race(LibretroCore core, int seed)
{
    // Goes up by one in every pass, from 0 to 27 and then again from 0 (the column guards respawn from).
    private const int PassCounter = 0x131E;
    private const int PassCounterValues = 28;

    private readonly Random _random = new(seed);
    private readonly Dictionary<Situation, int> _seen = [];

    public IReadOnlyDictionary<Situation, int> Seen => _seen;

    // Plays from where the game is, which must be a state that can be read, until the level ends or the frames are up.
    // Plays at random, or as told. Gives null, or the first difference.
    public string? Run(int frames, Func<GameState, Joystick>? play = null)
    {
        if (!GameState.TryRead(core.SystemRam, out var state))
            return "There was no state to start from.";

        // Which of the last few inputs the game looked at for a pass isn't known, so each is tried, and what doesn't
        // match the next state read is dropped.
        List<Joystick> recent = [Joystick.None];
        var pass = core.SystemRam[PassCounter];
        var held = Joystick.None;
        var heldUntil = 0;
        try
        {
            for (var frame = 0; frame < frames; frame++)
            {
                if (play is not null || frame >= heldUntil)
                {
                    Release(held);
                    held = play?.Invoke(state) ?? Pick(state);
                    Press(held);
                    heldUntil = frame + _random.Next(1, 18);
                }

                core.RunFrame();
                recent.Add(held);
                if (!GameState.TryRead(core.SystemRam, out var read))
                    continue;

                // The game completes a level before it counts the pass.
                var passes = (core.SystemRam[PassCounter] - pass + PassCounterValues) % PassCounterValues;
                pass = core.SystemRam[PassCounter];
                if (read.Completed)
                    passes++;

                var expected = After(state, recent, passes);
                var next = expected.FirstOrDefault(candidate => IsSame(candidate, read));
                if (next is null)
                    return Difference(state, recent, passes, expected, read);

                Note(state, next, held);
                if (next.Completed || !next.Alive)
                    return null;

                state = next;
                if (passes > 0)
                    recent = recent[^Math.Min(2, recent.Count)..];
            }

            return null;
        }
        finally
        {
            Release(held);
        }
    }

    private static List<GameState> After(GameState state, List<Joystick> inputs, int passes)
    {
        List<GameState> states = [state];
        for (var pass = 0; pass < passes; pass++)
        {
            states = states
                .SelectMany(before => inputs.Distinct().Select(before.Next))
                .DistinctBy(Describe)
                .ToList();
        }

        return states;
    }

    // Once the level has ended, the game soon changes the lives, the score and the level number, which Next doesn't.
    private static bool IsSame(GameState expected, GameState read) =>
        read.Alive && !read.Completed
            ? Describe(expected) == Describe(read)
            : DescribeEnd(expected) == DescribeEnd(read);

    private static string Describe(GameState state) =>
        $"{state.Player} level {state.Level.Number}, hidden ladders {string.Join(' ', state.Level.HiddenLadders)}, " +
        $"lives {state.Lives}, score {state.Score}, {DescribeEnd(state)}";

    private static string DescribeEnd(GameState state) =>
        $"player at {state.Player.Position} {state.Player.OffsetX},{state.Player.OffsetY}, alive {state.Alive}, " +
        $"completed {state.Completed}, gold {state.GoldLeft}, holes " +
        string.Join(' ', state.Holes.OrderBy(hole => (hole.Position.Row, hole.Position.Column))) + $"\n{state.Level}";

    private static string Difference(
        GameState before, List<Joystick> inputs, int passes, List<GameState> expected, GameState read) =>
        $"""
        After {passes} passes with {string.Join(" / ", inputs.Distinct())}, from
        {Describe(before)}
        the game had
        {Describe(read)}
        and Next gave
        {string.Join("\nor\n", expected.Select(Describe))}
        """;

    // Random, but often digs where it's rare to be able to.
    private Joystick Pick(GameState state)
    {
        var on = state.Level[state.Player.Position];
        if ((on is Tile.Rope or Tile.Ladder || state.Holes.Count > 0)
            && state.Next(Joystick.Fire).Player.Digging is not null && _random.Next(4) != 0)
        {
            return Joystick.Fire;
        }

        return _random.Next(22) switch
        {
            < 5 => Joystick.Left,
            < 10 => Joystick.Right,
            < 12 => Joystick.Up,
            < 14 => Joystick.Down,
            < 16 => Joystick.Fire,
            16 => Joystick.Up | Joystick.Left,
            17 => Joystick.Up | Joystick.Right,
            18 => Joystick.Down | Joystick.Left,
            19 => Joystick.Down | Joystick.Right,
            20 => Joystick.Fire | Joystick.Up,
            _ => Joystick.None,
        };
    }

    private static bool IsBesideOrBelow(Hole hole, Player player) =>
        hole.Position.Row == player.Position.Row + 1 && Math.Abs(hole.Position.Column - player.Position.Column) <= 1;

    private void Note(GameState before, GameState after, Joystick held)
    {
        var (player, was) = (after.Player, before.Player);
        var (column, row) = player.Position;
        var on = after.Level[player.Position];
        var below = row < Level.Height - 1 ? after.Level[column, row + 1] : Tile.Solid;
        var moved = (player.Position, player.OffsetX, player.OffsetY) != (was.Position, was.OffsetX, was.OffsetY);
        var dug = after.Holes.Any(hole => before.Holes.All(old => old.Position != hole.Position));
        var closed = before.Holes.Where(hole => after.Holes.All(now => now.Position != hole.Position)).ToList();

        Count(Situation.Falling, player.Falling);
        Count(Situation.FallingThroughATrapBrick, player.Falling && on == Tile.TrapBrick);
        Count(Situation.FallingIntoAHole, player.Falling && after.Holes.Any(hole => hole.Position == player.Position));
        Count(Situation.CaughtByARope, was.Falling && !player.Falling && on == Tile.Rope);
        Count(Situation.LandedOnTheGround, was.Falling && !player.Falling && on != Tile.Rope);
        Count(Situation.MovingOnARope, moved && on == Tile.Rope && !player.Falling);
        Count(Situation.MovingOnALadder, moved && on == Tile.Ladder);
        Count(Situation.MovingOnTheTopRow, moved && row == 0);
        Count(Situation.MovingOnTheBottomRow, moved && row == Level.Height - 1);
        Count(Situation.MovingAtTheLevelsSide, moved && column is 0 or Level.Width - 1);
        Count(Situation.HeldDiagonally, (int)(held & (Joystick.Up | Joystick.Down)) != 0
            && (int)(held & (Joystick.Left | Joystick.Right)) != 0);
        Count(Situation.DugOnTheFloor, dug && on == Tile.Empty);
        Count(Situation.DugOnALadder, dug && on == Tile.Ladder);
        Count(Situation.DugHangingOnARope, dug && on == Tile.Rope && below == Tile.Empty);
        Count(Situation.DugWithAnotherHoleOpen, dug && after.Holes.Count > 1);
        Count(Situation.HoleClosed, closed.Count > 0);
        Count(Situation.HoleClosedBesideOrBelowHim, after.Alive && closed.Any(hole => IsBesideOrBelow(hole, player)));
        Count(Situation.DiedInAHole, !after.Alive);
        Count(Situation.GoldTaken, after.GoldLeft < before.GoldLeft);
        Count(Situation.GoldTakenInAFall, after.GoldLeft < before.GoldLeft && was.Falling);
        Count(
            Situation.ExitLaddersShown,
            after.Level.HiddenLadders.Count < before.Level.HiddenLadders.Count);
        Count(Situation.LevelCompleted, after.Completed);
    }

    private void Count(Situation situation, bool seen)
    {
        if (seen)
            _seen[situation] = _seen.GetValueOrDefault(situation) + 1;
    }

    private void Press(Joystick joystick)
    {
        foreach (var button in Buttons(joystick))
            core.PressButton(button);
    }

    private void Release(Joystick joystick)
    {
        foreach (var button in Buttons(joystick))
            core.ReleaseButton(button);
    }

    private static IEnumerable<RetroPadButton> Buttons(Joystick joystick)
    {
        if (joystick.HasFlag(Joystick.Up))
            yield return RetroPadButton.Up;
        if (joystick.HasFlag(Joystick.Down))
            yield return RetroPadButton.Down;
        if (joystick.HasFlag(Joystick.Left))
            yield return RetroPadButton.Left;
        if (joystick.HasFlag(Joystick.Right))
            yield return RetroPadButton.Right;
        if (joystick.HasFlag(Joystick.Fire))
            yield return RetroPadButton.B;
    }

    internal enum Situation
    {
        Falling,
        FallingThroughATrapBrick,
        FallingIntoAHole,
        CaughtByARope,
        LandedOnTheGround,
        MovingOnARope,
        MovingOnALadder,
        MovingOnTheTopRow,
        MovingOnTheBottomRow,
        MovingAtTheLevelsSide,
        HeldDiagonally,
        DugOnTheFloor,
        DugOnALadder,
        DugHangingOnARope,
        DugWithAnotherHoleOpen,
        HoleClosed,
        HoleClosedBesideOrBelowHim,
        DiedInAHole,
        GoldTaken,
        GoldTakenInAFall,
        ExitLaddersShown,
        LevelCompleted,
    }
}
