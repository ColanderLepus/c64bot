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

    // How many pits are tried, at most; how many places the walk to where he digs each hole looks at; and how many the
    // search on to gold from where he lands.
    private const int Pits = 8;
    private const int WalkPlaces = 3_000;
    private const int OnFromAPit = 20_000;

    // How many places the search from where he is looks at first, for what's near, before walking first, then digging.
    private const int NearPlaces = 50_000;

    // From how many places he walks to, at most, a search with digging is tried, and how many places it looks at. On
    // level 10, the pit into the box from above it takes about 70,000.
    private const int Spots = 4;
    private const int SpotPlaces = 150_000;

    // How many places the short search looks at, from where a route ends, to make sure he can get on from there.
    private const int OnwardPlaces = 2_000;

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
    /// Finds a route from this state to the next gold he can take without walking into a trap, or once all the gold is
    /// taken, to the top row, which completes the level. Gives null if there's none, or if none is found among the
    /// first 400,000 places looked at.
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
    /// A route to gold is a trap if, from where it ends, he can no longer get to gold he could get to at its start, or
    /// to the top row once the exit ladders are there (<see cref="LevelGraph.WithinReach"/>), or if a short search from
    /// there, of up to 2,000 places, runs out of places to look at without finding a way on that isn't a trap in the
    /// same way. Then the search goes on to the next route: to other gold, or to the same gold another way. It doesn't
    /// look further ahead than that, so a trap that only shows two gold later isn't seen.
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

        var graph = new LevelGraph(start);
        var ways = Ways.From(start, graph);
        return (ways is null ? null : SearchTheGraph(start, graph, ways))
            ?? ThroughAPit(start, graph)
            ?? Search(start, graph, NearPlaces, searchOn: true).Route
            ?? (ways is null ? null : WalkThenDig(start, graph, ways))
            ?? Search(start, graph, MaxPlaces, searchOn: true).Route;
    }

    public override string ToString() => $"{Moves.Count} moves, {Passes.Count} passes: {string.Join("; ", Moves)}";

    // Searches the graph for a route from place to place, along runs and through digs he drops straight through, which
    // takes no time at all. The graph has no holes, so each route found is played pass by pass by the game's rules: it's
    // only taken if every move ends where it does in the graph. And it's checked for traps as Find says. Gives null if
    // none is found that way.
    private static Route? SearchTheGraph(GameState start, LevelGraph graph, Ways ways)
    {
        var traps = start.GoldLeft > 0 ? new Traps(start, graph, graph.WithinReach(start), searchOn: true) : null;
        foreach (var known in ways.ToGoals())
        {
            if (Played(start, known, toTheGoal: true) is { } moves
                && (traps is null || !traps.IsTrapped(moves[^1].End)))
            {
                return new Route(moves);
            }
        }

        return null;
    }

    // Through a pit (the owner's rule, see Pit): of the pits whose landing leads on to gold, those with the fewest holes
    // first, the route through each, put together straight from the pit. Gives null if none is found that way.
    private static Route? ThroughAPit(GameState start, LevelGraph graph)
    {
        if (start.GoldLeft == 0)
            return null;

        var leading = LeadingToGold(start, graph);
        var traps = new Traps(start, graph, graph.WithinReach(start), searchOn: true);
        var pits = Pit.AllIn(start, graph)
            .Where(pit => leading.Contains(pit.LandsAt) || pit.FallsPast.Any(tile => start.Level[tile] == Tile.Gold))
            .OrderBy(pit => pit.Digs.Count)
            .Take(Pits);
        foreach (var pit in pits)
        {
            if (Through(start, graph, pit, traps) is { } route)
                return route;
        }

        return null;
    }

    // The route through the pit: to where he digs each hole, the dig, and at the end a step over the bottom, which drops
    // him through it; then on to gold from where he lands. Each move is worked out by the game's rules, and if one
    // doesn't go as the pit says, as when a hole closes too soon, there's no route.
    private static Route? Through(GameState start, LevelGraph graph, Pit pit, Traps traps)
    {
        var moves = new List<Move>();
        var state = start;
        var (dig, over) = pit.Side == Direction.Right
            ? (MoveKind.DigLeft, MoveKind.Left)
            : (MoveKind.DigRight, MoveKind.Right);
        foreach (var (hole, from) in pit.Digs)
        {
            if (WalkTo(state, from) is not { } walk)
                return null;

            moves.AddRange(walk);
            state = walk.Count > 0 ? walk[^1].End : state;
            if (Move.AllFrom(state).FirstOrDefault(move => move.Kind == dig) is not { } dug
                || dug.End.Holes.All(open => open.Position != hole))
            {
                return null;
            }

            moves.Add(dug);
            state = dug.End;
        }

        if (Move.AllFrom(state).FirstOrDefault(move => move.Kind == over) is not { } drop)
            return null;

        moves.Add(drop);
        state = drop.End;
        if (state.GoldLeft < start.GoldLeft)
            return traps.IsTrapped(state) ? null : new Route(moves);

        if (state.Player.Position != pit.LandsAt)
            return null;

        return Search(state, graph, OnFromAPit, searchOn: true, traps).Route is { } rest
            ? new Route([.. moves, .. rest.Moves])
            : null;
    }

    // The quickest walk, without digging or taking gold, to the middle of a tile, worked out move by move by the game's
    // rules. Empty if he's there; null if it isn't found among a few thousand places.
    private static List<Move>? WalkTo(GameState start, TilePosition tile)
    {
        if (IsAt(start))
            return [];

        var seen = new HashSet<(TilePosition, int, int)> { Key(start) };
        var walks = new PriorityQueue<Step, int>();
        walks.Enqueue(new Step(start, null, null, 0), 0);
        while (walks.TryDequeue(out var step, out _) && seen.Count < WalkPlaces)
        {
            foreach (var move in Move.AllFrom(step.State))
            {
                if (move.Kind is MoveKind.DigLeft or MoveKind.DigRight or MoveKind.WaitForHole
                    || move.End.GoldLeft < start.GoldLeft || move.End.Completed || !seen.Add(Key(move.End)))
                {
                    continue;
                }

                var next = new Step(move.End, step, move, step.Passes + move.Passes.Count);
                if (IsAt(move.End))
                    return next.Moves();

                walks.Enqueue(next, next.Passes);
            }
        }

        return null;

        bool IsAt(GameState state) => state.Player is { OffsetX: 0, OffsetY: 0 } player && player.Position == tile;

        static (TilePosition, int, int) Key(GameState state) =>
            (state.Player.Position, state.Player.OffsetX, state.Player.OffsetY);
    }

    // The places from which the graph's runs lead to gold, as the runs are in the graph.
    private static HashSet<TilePosition> LeadingToGold(GameState start, LevelGraph graph)
    {
        var leading = new HashSet<TilePosition>();
        var into = new Dictionary<TilePosition, List<TilePosition>>();
        var next = new Queue<TilePosition>();
        foreach (var place in graph.Places)
        {
            foreach (var run in graph.RunsFrom(place))
            {
                if (IsAGoal(start, run))
                {
                    if (leading.Add(place))
                        next.Enqueue(place);
                }
                else if (into.TryGetValue(run.To, out var from))
                {
                    from.Add(place);
                }
                else
                {
                    into[run.To] = [place];
                }
            }
        }

        while (next.TryDequeue(out var place))
        {
            foreach (var from in into.GetValueOrDefault(place, []))
            {
                if (leading.Add(from))
                    next.Enqueue(from);
            }
        }

        return leading;
    }

    // Whether the run takes gold, or completes the level once there's none left, as the run is in the graph.
    private static bool IsAGoal(GameState start, Run run) =>
        start.GoldLeft > 0
            ? Enumerable.Range(0, run.Moves.Count).Any(move => run.TilesOn(move).Any(tile => start.Level[tile] == Tile.Gold))
            : run.Moves[^1].End.Completed;

    // Walks first, then digs (the owner's idea): from places he can walk to along the graph, a search with digging, from
    // the places where the gold seems nearest first, counting a brick dug into from the side as all the bricks above it.
    // The route is the walk and the digging, checked for traps as Find says. Gives null if none is found that way.
    private static Route? WalkThenDig(GameState start, LevelGraph graph, Ways ways)
    {
        var withinReach = graph.WithinReach(start);
        var estimate = new Estimate(start, withinReach, pitted: true);
        var traps = start.GoldLeft > 0 ? new Traps(start, graph, withinReach, searchOn: true) : null;
        var spots = new List<(int Least, List<Move> Walk, GameState There)>();
        foreach (var place in ways.Places)
        {
            // A walk that takes gold on the way is a route of its own, which the graph search has looked at.
            if (Played(start, ways.To(place), toTheGoal: false) is not { } walk)
                continue;

            var there = walk.Count > 0 ? walk[^1].End : start;
            if (estimate.LeastPasses(there) is { } least)
                spots.Add((least, walk, there));
        }

        // Where he is has been searched from already.
        foreach (var (_, walk, there) in spots.Where(spot => spot.Walk.Count > 0).OrderBy(spot => spot.Least).Take(Spots))
        {
            if (Search(there, graph, SpotPlaces, searchOn: true, traps).Route is { } dig)
                return new Route([.. walk, .. dig.Moves]);
        }

        return null;
    }

    // The moves that playing the graph's moves gives, pass by pass, by the game's rules. To the goal: up to where he
    // takes gold or completes the level, and null if he doesn't. Otherwise all of them, and null if he takes gold or
    // completes the level on the way. Null too if a move doesn't end where it does in the graph, as when a hole is in
    // the way.
    private static List<Move>? Played(GameState start, IEnumerable<Move> graphMoves, bool toTheGoal)
    {
        var moves = new List<Move>();
        var state = start;
        foreach (var known in graphMoves)
        {
            // A dig is worked out afresh: which way fire digs depends on his last step left or right.
            var move = known.Kind is MoveKind.DigLeft or MoveKind.DigRight
                ? Move.AllFrom(state).FirstOrDefault(move => move.Kind == known.Kind)
                : Move.Replay(state, known);
            if (move is null || move.End.Player.Position != known.End.Player.Position
                || (move.End.Player.OffsetX, move.End.Player.OffsetY) != (known.End.Player.OffsetX, known.End.Player.OffsetY))
            {
                return null;
            }

            moves.Add(move);
            state = move.End;
            if (start.GoldLeft > 0 ? state.GoldLeft < start.GoldLeft : state.Completed)
                return toTheGoal ? moves : null;
        }

        return toTheGoal ? null : moves;
    }

    // Searches as Find says, on a graph of the level as it is in the state, and gives up after so many places. Without
    // searching on, it only rules out a route as a trap with WithinReach. The traps can be those of an earlier state,
    // such as where a walk to this one started.
    private static (Route? Route, bool GaveUp) Search(
        GameState start, LevelGraph graph, int maxPlaces, bool searchOn, Traps? traps = null)
    {
        var withinReach = graph.WithinReach(start);
        var estimate = new Estimate(start, withinReach);
        if (estimate.LeastPasses(start) is not { } least)
            return (null, false);

        traps ??= start.GoldLeft > 0 ? new Traps(start, graph, withinReach, searchOn) : null;
        var earliest = new Dictionary<Place, int> { [Place.Of(start)] = 0 };
        var routes = new PriorityQueue<Step, (int Passes, int Taken)>();
        routes.Enqueue(new Step(start, null, null, 0), (Weight * least, 0));
        while (routes.TryDequeue(out var step, out _))
        {
            if (step.Move is not null && estimate.IsReached(step.State))
            {
                if (traps is null || !traps.IsTrapped(step.State))
                    return (new Route(step.Moves()), false);

                continue; // he'd be trapped there, so on to the next route
            }

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
                    if (earliest.Count > maxPlaces)
                        return (null, true);
                }
                else
                {
                    least = 0;
                }

                // Of routes as promising, the one furthest along first.
                routes.Enqueue(next, (next.Passes + Weight * least, -next.Passes));
            }
        }

        return (null, false);
    }

    // The quickest ways along the graph's runs from where he is: to each place he can get to, and to each run there that
    // takes gold, or completes the level, as the runs are in the graph.
    private sealed class Ways
    {
        private readonly TilePosition _from;

        // How he gets to each place he can get to: on which run, and after how many of its moves. A place he passes on
        // a run is one too, as somewhere to stop.
        private readonly Dictionary<TilePosition, (Run Run, int Moves)> _cameBy = [];
        private readonly List<(int Passes, Run Run)> _goals = [];

        private Ways(GameState start, LevelGraph graph)
        {
            _from = start.Player.Position;
            var passes = new Dictionary<TilePosition, int> { [_from] = 0 };
            var next = new PriorityQueue<TilePosition, int>();
            next.Enqueue(_from, 0);
            while (next.TryDequeue(out var place, out var sofar))
            {
                if (sofar > passes[place])
                    continue;

                foreach (var run in graph.RunsFrom(place))
                {
                    if (IsAGoal(start, run))
                    {
                        _goals.Add((sofar + run.Passes.Count, run));
                        continue;
                    }

                    // The places on the way, where he can stop, but which only lead on along the run; then its end.
                    var then = sofar;
                    for (var moves = 1; moves <= run.Moves.Count; moves++)
                    {
                        then += run.Moves[moves - 1].Passes.Count;
                        var at = run.Moves[moves - 1].End.Player.Position;
                        var isTheEnd = moves == run.Moves.Count;
                        if ((!isTheEnd && run.Way is MoveKind.DigLeft or MoveKind.DigRight)
                            || (passes.TryGetValue(at, out var known) && known <= then))
                        {
                            continue;
                        }

                        (passes[at], _cameBy[at]) = (then, (run, moves));
                        if (isTheEnd)
                            next.Enqueue(at, then);
                    }
                }
            }

            Places = passes.Keys;
        }

        public IReadOnlyCollection<TilePosition> Places { get; }

        // Null if he isn't in the middle of a place.
        public static Ways? From(GameState start, LevelGraph graph) =>
            start.Player is { OffsetX: 0, OffsetY: 0 } && graph.IsPlace(start.Player.Position)
                ? new Ways(start, graph)
                : null;

        // The moves to each goal, the quickest first, as they are in the graph.
        public IEnumerable<List<Move>> ToGoals() =>
            _goals.OrderBy(goal => goal.Passes).Select(goal => (List<Move>)[.. To(goal.Run.From), .. goal.Run.Moves]);

        // The moves to a place, as they are in the graph.
        public List<Move> To(TilePosition place)
        {
            var moves = new List<Move>();
            for (var at = place; at != _from; at = _cameBy[at].Run.From)
                moves.InsertRange(0, _cameBy[at].Run.Moves.Take(_cameBy[at].Moves));

            return moves;
        }

    }

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

    // Whether, where a route that takes gold ends, he can no longer get to gold he could get to at its start, or to the
    // top row once the exit ladders are there, if he could at the start. Gold or a way out that he couldn't get to at
    // the start doesn't count: no route can change that. Worked out with WithinReach, which never leaves out a tile he
    // can get to. And, searching on, whether a short search from there runs out of places to look at without finding a
    // way on that isn't a trap by WithinReach. So a route ruled out is a trap for sure.
    private sealed class Traps(GameState start, LevelGraph graph, IReadOnlySet<TilePosition> withinReach, bool searchOn)
    {
        private readonly TilePosition[] _gold = [.. GoldIn(start.Level).Where(withinReach.Contains)];
        private LevelGraph? _withExitLadders;
        private bool _wayOut;

        public bool IsTrapped(GameState end)
        {
            if (end.GoldLeft > 0)
            {
                var fromEnd = graph.WithinReach(end);
                if (_gold.Any(gold => end.Level[gold] == Tile.Gold && !fromEnd.Contains(gold)))
                    return true;
            }

            // The exit ladders appear when the last gold is taken, which the game does in the same pass. Until then,
            // the graph has them, and the level he digs in doesn't, so both count.
            if (_withExitLadders is null)
            {
                _withExitLadders = new LevelGraph(WithExitLadders(end));
                _wayOut = ReachesTheTop(start);
            }

            if (_wayOut && !ReachesTheTop(end))
                return true;

            // On to the rest he could get to at the start, if there's any.
            var more = end.GoldLeft > 0 ? _gold.Any(gold => end.Level[gold] == Tile.Gold) : _wayOut;
            if (!searchOn || end.Completed || !more)
                return false;

            var onward = Search(end, end.GoldLeft > 0 ? graph : _withExitLadders, OnwardPlaces, searchOn: false);
            return onward is { Route: null, GaveUp: false };
        }

        private bool ReachesTheTop(GameState state) => _withExitLadders!.WithinReach(state).Any(tile => tile.Row == 0);

        private static IEnumerable<TilePosition> GoldIn(Level level) =>
            Enumerable.Range(0, Level.Width * Level.Height)
                .Select(i => new TilePosition(i % Level.Width, i / Level.Width))
                .Where(position => level[position] == Tile.Gold);

        // The state after a pass with no gold left, in which the game shows the exit ladders.
        private static GameState WithExitLadders(GameState state) =>
            new GameState(
                    state.Level, state.Player, state.Guards, [], state.Lives, state.Score, goldLeft: 0, alive: true,
                    completed: false)
                .Next(Joystick.None);
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
    // Pitted, it's no longer the least, but a guess at the passes, for telling where to dig from: into a brick from the
    // side, he digs not only it, but every brick above it, since a brick is dug from above (the owner's pit); and he only
    // goes to the side from a tile where something holds him: a ladder, a rope, or a floor below.
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

        private readonly bool _pitted;

        public Estimate(GameState start, IReadOnlySet<TilePosition> withinReach, bool pitted = false)
        {
            _pitted = pitted;
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
                || (Ways[way].Row == -1 && TileNow(from) != Tile.Ladder)
                || (_pitted && Ways[way].Row == 0 && !IsHeld(from)))
            {
                return;
            }

            if (tile == Tile.Brick)
                passes += Digging * (_pitted && Ways[way].Row == 0 ? BricksFrom(to) : 1);

            var node = to * Ways.Length + way;
            if (_fromEstimate[node] == _estimate && _passes[node] <= passes)
                return;

            (_fromEstimate[node], _passes[node]) = (_estimate, passes);
            _next.Enqueue(node, passes);
        }

        private Tile TileNow(int tile) => _open[tile] ? Tile.Empty : _tiles[tile];

        private bool IsHeld(int tile) =>
            TileNow(tile) is Tile.Ladder or Tile.Rope
            || tile >= Tiles - Level.Width
            || TileNow(tile + Level.Width) is Tile.Brick or Tile.Solid or Tile.Ladder;

        // The bricks from this one up, one above another.
        private int BricksFrom(int tile)
        {
            var bricks = 0;
            for (var above = tile; above >= 0 && TileNow(above) == Tile.Brick; above -= Level.Width)
                bricks++;

            return bricks;
        }

        private static int TileOf(TilePosition position) => position.Row * Level.Width + position.Column;
    }
}
