using C64Bot.Libretro;

namespace C64Bot.LodeRunner;

/// <summary>
/// A level as a graph: the places where the player can stop, and the runs between them.
/// </summary>
/// <remarks>
/// <para>
/// A place is the middle of a tile where something holds him, so that the joystick decides what he does next: he
/// stands on a floor, is on a ladder, or hangs from a rope. A run goes from a place one way, left, right, up or down,
/// to the next place where he can do more than carry on that way or turn back: where a ladder or a rope starts or
/// ends, where he can climb down or let go, at gold, against a wall, or where a fall lands him. On the way he passes
/// places where all he can do is carry on, turn back, or dig. A dig is a run too, where he drops straight through the
/// hole: from the place he digs from, to the place where he lands.
/// </para>
/// <para>
/// It's worked out with the game's rules (<see cref="Move"/>, built on <see cref="GameState.Next"/>) on the level as it
/// is in a state, but without its holes and gold. Gold doesn't change how he moves, so the graph holds while he takes
/// it, until the exit ladders appear. Holes do change where he can go: <see cref="WithinReach"/> takes them in.
/// </para>
/// </remarks>
public sealed class LevelGraph
{
    private static readonly MoveKind[] Ways = [MoveKind.Left, MoveKind.Right, MoveKind.Up, MoveKind.Down];

    private readonly Dictionary<TilePosition, IReadOnlyList<Run>> _runs = [];
    private readonly Level _level;

    /// <param name="state">
    /// A state of the level: its tiles, with the exit ladders if they're there. Where the player is, the holes, and
    /// what gold is left don't matter, but for where the gold is, at which runs end.
    /// </param>
    /// <exception cref="NotSupportedException">The level has guards, which the rules don't have yet.</exception>
    public LevelGraph(GameState state)
    {
        _level = state.Level;
        // Each place, with its moves on the level without holes and gold.
        var level = state.Level.Replace(Tile.Gold, Tile.Empty);
        var moves = new Dictionary<TilePosition, Dictionary<MoveKind, (Move Move, TilePosition[] Tiles)>>();
        var digs = new List<(TilePosition Place, GameState There, Move Dig)>();
        for (var row = 0; row < Level.Height; row++)
        {
            for (var column = 0; column < Level.Width; column++)
            {
                // With no gold left, the top row completes the level, so it's no place to stop.
                var position = new TilePosition(column, row);
                if (level[position] is Tile.Brick or Tile.Solid || (row == 0 && state.GoldLeft == 0))
                    continue;

                var there = StateAt(state, level, position);
                if (there.Next(Joystick.None).Player.Falling)
                    continue;

                var all = Move.AllFrom(there);
                moves[position] = all
                    .Where(move => Ways.Contains(move.Kind))
                    .ToDictionary(move => move.Kind, move => (move, TilesOn(there, move)));
                digs.AddRange(all.Where(move => move.Kind is MoveKind.DigLeft or MoveKind.DigRight)
                    .Select(dig => (position, there, dig)));
            }
        }

        foreach (var (place, from) in moves)
            _runs[place] = [.. from.Keys.Select(way => Follow(place, way, moves, state.Level))];

        // A dig, and the step that way that drops him through the hole, to where he lands, if that's a place.
        foreach (var (place, there, dig) in digs)
        {
            var way = dig.Kind == MoveKind.DigLeft ? MoveKind.Left : MoveKind.Right;
            if (Move.AllFrom(dig.End).FirstOrDefault(move => move.Kind == way) is { End.Player: { OffsetX: 0, OffsetY: 0 } lands } drop
                && moves.ContainsKey(lands.Position) && lands.Position.Row > place.Row)
            {
                _runs[place] = [.. _runs[place], new Run(place, dig.Kind, [dig, drop], [TilesOn(there, dig), TilesOn(dig.End, drop)])];
            }
        }
    }

    public IReadOnlyCollection<TilePosition> Places => _runs.Keys;

    /// <summary>
    /// Gives the runs from a place, one for each way he can go from there, and one for each dig he can drop straight
    /// through; none if it's not a place.
    /// </summary>
    public IReadOnlyList<Run> RunsFrom(TilePosition place) => _runs.GetValueOrDefault(place, []);

    /// <summary>
    /// Works out the tiles he could ever get to from this state, digging included. It can give tiles he can't really
    /// get to, but never leaves out one he can: what isn't among them, he can't get to.
    /// </summary>
    /// <remarks>
    /// He goes from place to place as the graph's moves take him, and into holes: a brick counts as a hole if he could
    /// dig it at all. By the game's rules, he digs a brick standing beside it on the tile above, which can be in a hole,
    /// and only when the tile above the brick is empty or a hole: not when there's a ladder or a rope on it, nor gold,
    /// but gold he can take first, so that counts as taken. From
    /// a hole, he can go into the holes beside it, or out to the side where there's no wall, and down where the brick
    /// below is dug or there's none. He can't climb out of one, and only gets up into one from a ladder below it. The
    /// holes open in the state count as dug. If the graph has ladders the state doesn't, such as the exit ladders,
    /// he can go either way, as if they were there and as if they weren't.
    /// </remarks>
    public IReadOnlySet<TilePosition> WithinReach(GameState state) => new Reach(this, state).Tiles;

    // Whether it's a place in the graph: the middle of a tile where something holds him.
    internal bool IsPlace(TilePosition tile) => _runs.ContainsKey(tile);

    // He stands, hangs or climbs in the middle of the tile, as when a move ends there.
    private static GameState StateAt(GameState state, Level level, TilePosition position) => new(
        level,
        new Player(position, 0, 0, Pose.Run, Direction.Right, false, null, 0, Direction.Right),
        state.Guards,
        [],
        state.Lives,
        state.Score,
        state.GoldLeft,
        alive: true,
        completed: false);

    // Carries on the same way from the place, past places where he can only carry on or turn back.
    private static Run Follow(
        TilePosition from,
        MoveKind way,
        Dictionary<TilePosition, Dictionary<MoveKind, (Move Move, TilePosition[] Tiles)>> moves,
        Level level)
    {
        var run = new List<(Move Move, TilePosition[] Tiles)> { moves[from][way] };
        var at = from;
        while (run[^1].Move.End is { Completed: false, Player: { OffsetX: 0, OffsetY: 0 } player }
            && player.Position == Next(at, way)
            && moves.TryGetValue(player.Position, out var there)
            && there.ContainsKey(way)
            && there.Keys.All(kind => kind == way || kind == Reverse(way))
            && level[player.Position] != Tile.Gold)
        {
            run.Add(there[way]);
            at = player.Position;
        }

        return new Run(from, way, [.. run.Select(step => step.Move)], [.. run.Select(step => step.Tiles)]);
    }

    // The tiles he's on, pass by pass, in a move from this state.
    private static TilePosition[] TilesOn(GameState start, Move move)
    {
        var tiles = new HashSet<TilePosition> { start.Player.Position };
        var state = start;
        foreach (var joystick in move.Passes)
        {
            state = state.Next(joystick);
            tiles.Add(state.Player.Position);
        }

        return [.. tiles];
    }

    // The tile next to this one that way. A move that ends anywhere else had a fall in it.
    private static TilePosition Next(TilePosition tile, MoveKind way) => way switch
    {
        MoveKind.Left => tile with { Column = tile.Column - 1 },
        MoveKind.Right => tile with { Column = tile.Column + 1 },
        MoveKind.Up => tile with { Row = tile.Row - 1 },
        _ => tile with { Row = tile.Row + 1 },
    };

    private static MoveKind Reverse(MoveKind way) => way switch
    {
        MoveKind.Left => MoveKind.Right,
        MoveKind.Right => MoveKind.Left,
        MoveKind.Up => MoveKind.Down,
        _ => MoveKind.Up,
    };

    // The tiles he could get to from a state, worked out as WithinReach says, tile by tile.
    private sealed class Reach
    {
        private readonly LevelGraph _graph;
        private readonly Level _level;
        private readonly HashSet<TilePosition> _reached = [];
        private readonly HashSet<TilePosition> _holes;
        private readonly Queue<TilePosition> _next = new();

        public Reach(LevelGraph graph, GameState state)
        {
            _graph = graph;
            _level = state.Level;
            _holes = [.. state.Holes.Select(hole => hole.Position)];
            Add(state.Player.Position);
            while (_next.TryDequeue(out var tile))
                GoOnFrom(tile);
        }

        public IReadOnlySet<TilePosition> Tiles => _reached;

        private void GoOnFrom(TilePosition tile)
        {
            var (column, row) = tile;
            if (_holes.Contains(tile))
            {
                Side(column - 1, row);
                Side(column + 1, row);
                Down(column, row);
                DigFrom(tile);
            }
            else if (_graph._runs.TryGetValue(tile, out var runs))
            {
                foreach (var run in runs)
                    foreach (var on in run.TilesOn(0))
                        Add(on);

                // Into holes, which the graph doesn't have: down, to the side, and up from a ladder.
                IntoAHole(column, row + 1);
                IntoAHole(column - 1, row);
                IntoAHole(column + 1, row);
                if (_level[tile] == Tile.Ladder || _graph._level[tile] == Tile.Ladder)
                    IntoAHole(column, row - 1);

                DigFrom(tile);
            }
            else
            {
                Down(column, row); // nothing holds him here, so he falls
            }
        }

        // Out of a hole to the side: into a hole, or onto a tile that isn't a wall.
        private void Side(int column, int row)
        {
            if (column is < 0 or >= Level.Width)
                return;

            var tile = new TilePosition(column, row);
            if (_holes.Contains(tile) || _level[tile] is not (Tile.Brick or Tile.Solid or Tile.TrapBrick))
                Add(tile);
        }

        // Down through a hole or a tile that doesn't hold him up.
        private void Down(int column, int row)
        {
            if (row + 1 >= Level.Height)
                return;

            var tile = new TilePosition(column, row + 1);
            if (_holes.Contains(tile) || _level[tile] is not (Tile.Brick or Tile.Solid))
                Add(tile);
        }

        private void IntoAHole(int column, int row)
        {
            var tile = new TilePosition(column, row);
            if (column is >= 0 and < Level.Width && row is >= 0 and < Level.Height && _holes.Contains(tile))
                Add(tile);
        }

        // Standing on this tile, he digs the bricks beside it below, if he can.
        private void DigFrom(TilePosition tile)
        {
            Dig(tile.Column - 1, tile.Row + 1);
            Dig(tile.Column + 1, tile.Row + 1);
        }

        private void Dig(int column, int row)
        {
            if (column is < 0 or >= Level.Width || row is < 1 or >= Level.Height)
                return;

            var brick = new TilePosition(column, row);
            var above = new TilePosition(column, row - 1);
            if (_level[brick] != Tile.Brick || _holes.Contains(brick)
                || !(_level[above] is Tile.Empty or Tile.Gold || _holes.Contains(above))
                || !(StandsOn(column - 1, row - 1) || StandsOn(column + 1, row - 1)))
            {
                return;
            }

            _holes.Add(brick);

            // He gets into it from above, from the side, or up a ladder below it, wherever he's been already.
            var below = new TilePosition(column, row + 1);
            if (_reached.Contains(above) || StandsOn(column - 1, row) || StandsOn(column + 1, row)
                || (row + 1 < Level.Height && _reached.Contains(below) && _level[below] == Tile.Ladder))
            {
                Add(brick);
            }

            // With this one dug, the brick below it can be dug too.
            Dig(column, row + 1);
        }

        // Whether he's been on this tile, standing or hanging there, or in a hole.
        private bool StandsOn(int column, int row)
        {
            var tile = new TilePosition(column, row);
            return column is >= 0 and < Level.Width && row >= 0
                && _reached.Contains(tile) && (_holes.Contains(tile) || _graph._runs.ContainsKey(tile));
        }

        private void Add(TilePosition tile)
        {
            if (_reached.Add(tile))
                _next.Enqueue(tile);
        }
    }
}
