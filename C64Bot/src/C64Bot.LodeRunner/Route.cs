using C64Bot.Libretro;

namespace C64Bot.LodeRunner;

/// <summary>
/// A route the player can take through a level: moves one after another, to where he takes the next gold, or
/// completes the level.
/// </summary>
/// <remarks>
/// Each move starts where the one before ended, and each is exact (see <see cref="Move"/>), so the route is too: held
/// pass by pass as its <see cref="Passes"/> say, the joystick takes him there.
/// </remarks>
public sealed class Route
{
    // How much more the passes still to go count than the passes taken, when choosing which route to try next. Above 1,
    // the search heads for the goal sooner, and a route it finds can be slower than the fastest, by up to this many
    // times.
    private const int Weight = 2;

    // How many places the search looks at before it gives up. On the 150 levels without guards, no route took more
    // than 380,000, and that many take about 15 seconds.
    private const int MaxPlaces = 400_000;

    private Route(IReadOnlyList<Move> moves)
    {
        Moves = moves;
        Passes = [.. moves.SelectMany(move => move.Passes)];
        End = moves[^1].End;
    }

    public IReadOnlyList<Move> Moves { get; }

    /// <summary>
    /// How the joystick is held in each pass of the route, one after another: the passes of its moves.
    /// </summary>
    public IReadOnlyList<Joystick> Passes { get; }

    /// <summary>
    /// The state at the end of the route, when he has taken the gold, or completed the level.
    /// </summary>
    public GameState End { get; }

    /// <summary>
    /// Finds a route from this state to the next gold, or once all the gold is taken, to the top row, which completes
    /// the level. Gives null if he can't get there, or if no route is found among the first 400,000 places looked at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It tries every move, digs and waits included, and the game's rules decide what works (D19). First it checks
    /// whether he can get there at all (<see cref="LevelGraph.WithinReach"/>), and if not, it doesn't search. Then it
    /// tries first the routes that could get there soonest, by the passes so far and the least it could take from
    /// there. The least is worked out on a simpler model of how he moves: tile by tile, to the side or down, up only on
    /// a ladder, only on tiles within reach, and digging each brick in the way. The passes still to go count twice,
    /// which makes the search head for the goal, so the route it finds can be slower than the fastest, up to twice as
    /// slow. Which gold it goes to is the one it finds a route to first, usually the nearest.
    /// </para>
    /// <para>
    /// To keep the search small, a place he has been at already, with the same holes open, isn't looked at again,
    /// whatever passes the holes have left. The first time he's there counts. So in principle a route can be missed
    /// that needs a hole dug later.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">He's digging, or the level has ended.</exception>
    /// <exception cref="NotSupportedException">The level has guards, which the rules don't have yet.</exception>
    public static Route? Find(GameState start)
    {
        if (start is not { Alive: true, Completed: false, Player.Digging: null })
            throw new ArgumentException("The level has ended, or he's digging.", nameof(start));

        if (start.Guards.Count > 0)
            throw new NotSupportedException("The rules don't have guards yet.");

        var estimate = new Estimate(start, new LevelGraph(start).WithinReach(start));
        if (estimate.LeastPasses(start) is not { } least)
            return null;

        var earliest = new Dictionary<Place, int> { [Place.Of(start)] = 0 };
        var routes = new PriorityQueue<Step, (int Passes, int Taken)>();
        routes.Enqueue(new Step(start, null, null, 0), (Weight * least, 0));
        while (routes.TryDequeue(out var step, out _))
        {
            if (step.Move is not null && estimate.IsReached(step.State))
                return new Route(step.Moves());

            if (earliest[Place.Of(step.State)] < step.Passes)
                continue; // he got there sooner on another route

            foreach (var move in Move.AllFrom(step.State))
            {
                var end = move.End;
                var next = new Step(end, step, move, step.Passes + move.Passes.Count);
                if (!estimate.IsReached(end))
                {
                    var place = Place.Of(end);
                    if (earliest.TryGetValue(place, out var passes) && passes <= next.Passes)
                        continue;

                    if (estimate.LeastPasses(end) is not { } still)
                        continue; // he can't get there from here

                    earliest[place] = next.Passes;
                    least = still;
                    if (earliest.Count > MaxPlaces)
                        return null;
                }
                else
                {
                    least = 0;
                }

                // Of routes as promising, the one furthest along first.
                routes.Enqueue(next, (next.Passes + Weight * least, -next.Passes));
            }
        }

        return null;
    }

    public override string ToString() => $"{Moves.Count} moves, {Passes.Count} passes: {string.Join("; ", Moves)}";

    // A route so far: the state at its end, the step before and the move from there, and how many passes it took.
    private sealed record Step(GameState State, Step? Before, Move? Move, int Passes)
    {
        public List<Move> Moves()
        {
            var moves = new List<Move>();
            for (var step = this; step.Move is not null; step = step.Before!)
                moves.Add(step.Move);

            moves.Reverse();
            return moves;
        }
    }

    // Where he is, which way fire digs, and which holes are open. Nothing else that the next passes depend on can be
    // different: the gold is the same until he takes some, which ends the route.
    private sealed record Place(TilePosition Position, int OffsetX, int OffsetY, Direction DigSide, TilePosition[] Holes)
    {
        public static Place Of(GameState state)
        {
            var player = state.Player;
            TilePosition[] holes =
                [.. state.Holes.Select(hole => hole.Position).OrderBy(hole => (hole.Row, hole.Column))];
            return new Place(player.Position, player.OffsetX, player.OffsetY, player.DigSide, holes);
        }

        public bool Equals(Place? other) =>
            other is not null
            && (Position, OffsetX, OffsetY, DigSide) == (other.Position, other.OffsetX, other.OffsetY, other.DigSide)
            && Holes.SequenceEqual(other.Holes);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Position);
            hash.Add(OffsetX);
            hash.Add(OffsetY);
            hash.Add(DigSide);
            foreach (var hole in Holes)
                hash.Add(hole);

            return hash.ToHashCode();
        }
    }

    // The goal, and the least passes he could need to get to it. Worked out on a simpler model of how he moves, in
    // which he can do all he can do in the game, and more, and never in fewer passes. In it, he goes from tile to tile,
    // to the side or down anywhere but into a solid block, and up only from a ladder, but only on the tiles within
    // reach from the start: he can't get to any other from anywhere he gets to. Into a brick that isn't dug, he first
    // digs it. The passes this takes at least, from the game's rules:
    // - Going on from a tile into the next, straight on: 5. He went in at its edge, and leaves it at the other.
    // - Turning: 2. He's within a step of the middle of the tile the other way, since moving one way centres him the
    //   other way, a step each pass.
    // - A dig: 11 more. Of its 13 passes, he only moves in the first two, to the middle of his tile.
    // - Taking gold, or getting level with the top row, once he's on its tile: 2, from the edge to the middle.
    private sealed class Estimate
    {
        private const int Tiles = Level.Width * Level.Height;
        private const int StraightOn = 5;
        private const int Turning = 2;
        private const int Digging = 11;
        private const int ToTheMiddle = 2;

        // The ways to go, in order: right, left, down, up.
        private static readonly (int Column, int Row)[] Ways = [(1, 0), (-1, 0), (0, 1), (0, -1)];

        private readonly int _goldLeft;
        private readonly Tile[] _tiles = new Tile[Tiles];
        private readonly bool[] _goal = new bool[Tiles];
        private readonly bool[] _withinReach = new bool[Tiles];

        // For each tile and the way he went into it, the least passes to get there. Reused: what's from an earlier
        // estimate is left in and marked by the estimate it's from.
        private readonly int[] _passes = new int[Tiles * Ways.Length];
        private readonly int[] _fromEstimate = new int[Tiles * Ways.Length];
        private readonly bool[] _open = new bool[Tiles];
        private readonly PriorityQueue<int, int> _next = new();
        private int _estimate;

        public Estimate(GameState start, IReadOnlySet<TilePosition> withinReach)
        {
            _goldLeft = start.GoldLeft;
            for (var i = 0; i < Tiles; i++)
            {
                var position = new TilePosition(i % Level.Width, i / Level.Width);
                _tiles[i] = start.Level[position];
                _withinReach[i] = withinReach.Contains(position);
                _goal[i] = _withinReach[i] && (_goldLeft > 0 ? _tiles[i] == Tile.Gold : i < Level.Width);
            }
        }

        public bool IsReached(GameState state) => _goldLeft > 0 ? state.GoldLeft < _goldLeft : state.Completed;

        // Null if he can't get there.
        public int? LeastPasses(GameState state)
        {
            var player = state.Player;
            var from = TileOf(player.Position);
            if (_goal[from])
                return _goldLeft > 0 ? Math.Max(Math.Abs(player.OffsetX), Math.Abs(player.OffsetY)) : Math.Abs(player.OffsetY);

            _estimate++;
            _next.Clear();
            foreach (var hole in state.Holes)
                _open[TileOf(hole.Position)] = true;

            // The first tile he goes into: from where he is in his own tile to its edge.
            int[] toTheEdge = [3 - player.OffsetX, 3 + player.OffsetX, 3 - player.OffsetY, 3 + player.OffsetY];
            for (var way = 0; way < Ways.Length; way++)
                Go(from, way, toTheEdge[way]);

            int? least = null;
            while (_next.TryDequeue(out var node, out var passes))
            {
                if (passes > _passes[node])
                    continue;

                var (tile, wentIn) = Math.DivRem(node, Ways.Length);
                if (_goal[tile])
                {
                    least = passes + ToTheMiddle;
                    break;
                }

                for (var way = 0; way < Ways.Length; way++)
                    Go(tile, way, passes + (way == wentIn ? StraightOn : Turning));
            }

            foreach (var hole in state.Holes)
                _open[TileOf(hole.Position)] = false;

            return least;
        }

        private void Go(int from, int way, int passes)
        {
            var (column, row) = (from % Level.Width + Ways[way].Column, from / Level.Width + Ways[way].Row);
            if (column is < 0 or >= Level.Width || row is < 0 or >= Level.Height)
                return;

            var to = row * Level.Width + column;
            if (!_withinReach[to])
                return;

            var tile = TileNow(to);
            if (tile == Tile.Solid || (tile == Tile.TrapBrick && Ways[way].Row != 1)
                || (Ways[way].Row == -1 && TileNow(from) != Tile.Ladder))
            {
                return;
            }

            if (tile == Tile.Brick)
                passes += Digging;

            var node = to * Ways.Length + way;
            if (_fromEstimate[node] == _estimate && _passes[node] <= passes)
                return;

            (_fromEstimate[node], _passes[node]) = (_estimate, passes);
            _next.Enqueue(node, passes);
        }

        private Tile TileNow(int tile) => _open[tile] ? Tile.Empty : _tiles[tile];

        private static int TileOf(TilePosition position) => position.Row * Level.Width + position.Column;
    }
}
