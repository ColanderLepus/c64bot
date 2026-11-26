namespace C64Bot.LodeRunner;

/// <summary>
/// A pit the player can dig to get down through bricks: the holes, in the order he digs them, worked out from the
/// level's layout by the owner's rule (D19).
/// </summary>
/// <remarks>
/// <para>
/// He drops down one column of bricks, at least two deep: its top brick is <see cref="Top"/>, and below its last one
/// he falls on to <see cref="LandsAt"/>. He can only dig a brick standing beside it on the tile above, and only with the
/// tile above it empty or dug. So to dig the column, he stands in holes in the column beside it, and to dig those, in
/// holes in the next column out, each column a layer shallower than the one before. The pit goes out to one side
/// (<see cref="Side"/>) until a ladder or rope gives him something to stand on at every row he digs from, or until the
/// layers run out: beside a ladder, a pit of four layers is the column alone; with nothing to stand on, it's 4 + 3 + 2
/// + 1 holes.
/// </para>
/// <para>
/// He digs layer by layer from the top. In each layer, the hole in the column he drops down comes first, then the
/// columns outward, since he stands on the brick he'd dig next. Once all are dug, a step toward the column drops him
/// through. Whether it works in the game, for example whether the holes stay open long enough, only the game's rules
/// can tell, move by move.
/// </para>
/// </remarks>
public sealed class Pit
{
    private Pit(TilePosition top, Direction side, IReadOnlyList<PitDig> digs, TilePosition landsAt, IReadOnlyList<TilePosition> fallsPast)
    {
        Top = top;
        Side = side;
        Digs = digs;
        LandsAt = landsAt;
        FallsPast = fallsPast;
    }

    /// <summary>
    /// The top brick of the column he drops down.
    /// </summary>
    public TilePosition Top { get; }

    /// <summary>
    /// Which side of the column he drops down the pit goes out to. He digs toward the other side.
    /// </summary>
    public Direction Side { get; }

    /// <summary>
    /// The holes in the order he digs them, each with the tile he stands on to dig it.
    /// </summary>
    public IReadOnlyList<PitDig> Digs { get; }

    /// <summary>
    /// Where he lands when he drops through the bottom of the pit.
    /// </summary>
    public TilePosition LandsAt { get; }

    /// <summary>
    /// The tiles he falls through between the bottom of the pit and where he lands, where he takes any gold.
    /// </summary>
    public IReadOnlyList<TilePosition> FallsPast { get; }

    /// <summary>
    /// Works out every pit he could dig in the level as it is in this state, by the rule in the remarks.
    /// </summary>
    /// <remarks>
    /// A pit is left out if a tile it needs isn't as it should be: a hole that isn't a brick (or is open already), a
    /// tile he digs from that isn't somewhere he can stand (a place in the graph), or a tile above a top hole that isn't
    /// empty. It's also left out if he'd stay in the bottom hole.
    /// </remarks>
    /// <param name="state">The state: its level and the holes open in it.</param>
    /// <param name="graph">The level as a graph, for where he can stand.</param>
    public static IReadOnlyList<Pit> AllIn(GameState state, LevelGraph graph)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(graph);
        var level = state.Level;
        var open = state.Holes.Select(hole => hole.Position).ToHashSet();
        var pits = new List<Pit>();
        for (var column = 0; column < Level.Width; column++)
        {
            for (var top = 1; top < Level.Height; top++)
            {
                if (!IsBrick(column, top) || !IsEmpty(column, top - 1))
                    continue;

                var layers = 0;
                while (IsBrick(column, top + layers))
                    layers++;

                if (layers < 2 || Landing(column, top + layers) is not { } landing)
                    continue;

                foreach (var side in (Direction[])[Direction.Left, Direction.Right])
                {
                    if (Digs(column, top, layers, side == Direction.Right ? 1 : -1) is { } digs)
                        pits.Add(new Pit(new TilePosition(column, top), side, digs, landing.At, landing.Past));
                }
            }
        }

        return pits;

        // The holes by the rule: how many each column out from the one he drops down has, then the order of the digs.
        List<PitDig>? Digs(int column, int top, int layers, int step)
        {
            var depths = new List<int> { layers };
            for (var columnsOut = 1; ; columnsOut++)
            {
                // He stands here to dig the column before: on the tile above the pit, and in each row down to one
                // above the column before's last hole.
                var x = column + step * columnsOut;
                if (x is < 0 or >= Level.Width || !graph.IsPlace(new TilePosition(x, top - 1)))
                    return null;

                var rows = depths[^1] - 1;
                if (rows == 0 || Enumerable.Range(top, rows).All(row => level[x, row] is Tile.Ladder or Tile.Rope))
                    break;

                // Nothing to stand on there: he stands in holes, a layer shallower.
                if (!IsEmpty(x, top - 1) || !Enumerable.Range(top, rows).All(row => IsBrick(x, row)))
                    return null;

                depths.Add(rows);
            }

            var digs = new List<PitDig>();
            for (var layer = 0; layer < layers; layer++)
            {
                for (var columnsOut = 0; columnsOut < depths.Count && layer < depths[columnsOut]; columnsOut++)
                {
                    digs.Add(new PitDig(
                        new TilePosition(column + step * columnsOut, top + layer),
                        new TilePosition(column + step * (columnsOut + 1), top + layer - 1)));
                }
            }

            return digs;
        }

        // Where he lands, falling from below the pit's last brick, and what he falls past on the way. Null if he'd
        // stay in the bottom hole.
        (TilePosition At, List<TilePosition> Past)? Landing(int column, int row)
        {
            var past = new List<TilePosition>();
            for (; row < Level.Height; row++)
            {
                var tile = new TilePosition(column, row);
                if (graph.IsPlace(tile))
                    return (tile, past);

                if (level[tile] is Tile.Brick or Tile.Solid)
                    return null;

                past.Add(tile);
            }

            return null;
        }

        bool IsBrick(int column, int row) =>
            column is >= 0 and < Level.Width && row is >= 0 and < Level.Height
            && level[column, row] == Tile.Brick && !open.Contains(new TilePosition(column, row));

        bool IsEmpty(int column, int row) => level[column, row] == Tile.Empty && !open.Contains(new TilePosition(column, row));
    }

    public override string ToString() =>
        $"Pit down from {Top}, out to the {Side}, {Digs.Count} holes, landing at {LandsAt}";
}

/// <summary>
/// A hole of a <see cref="Pit"/>, and the tile the player stands on to dig it.
/// </summary>
public readonly record struct PitDig(TilePosition Hole, TilePosition From);
