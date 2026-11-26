using C64Bot.Libretro;

namespace C64Bot.LodeRunner;

/// <summary>
/// A run in a <see cref="LevelGraph"/>: from a place one way, to the next place where the player can do more than carry
/// on that way or turn back.
/// </summary>
public sealed class Run
{
    // The tiles he's on in the passes of each move.
    private readonly IReadOnlyList<TilePosition[]> _tiles;

    internal Run(TilePosition from, MoveKind way, IReadOnlyList<Move> moves, IReadOnlyList<TilePosition[]> tiles)
    {
        From = from;
        Way = way;
        Moves = moves;
        Passes = [.. moves.SelectMany(move => move.Passes)];
        To = moves[^1].End.Player.Position;
        _tiles = tiles;
    }

    public TilePosition From { get; }

    /// <summary>
    /// Which way he goes: <see cref="MoveKind.Left"/>, <see cref="MoveKind.Right"/>, <see cref="MoveKind.Up"/> or
    /// <see cref="MoveKind.Down"/>.
    /// </summary>
    public MoveKind Way { get; }

    /// <summary>
    /// The moves of the run, one tile after another, as they are in the level without holes and gold. Each but the
    /// last ends at a place he passes on the way.
    /// </summary>
    public IReadOnlyList<Move> Moves { get; }

    /// <summary>
    /// How the joystick is held in each pass of the run: the passes of its moves.
    /// </summary>
    public IReadOnlyList<Joystick> Passes { get; }

    /// <summary>
    /// Where the run ends: in the middle of this tile, or where the level is completed.
    /// </summary>
    public TilePosition To { get; }

    public override string ToString() => $"{Way} from {From} to {To}, {Moves.Count} moves, {Passes.Count} passes";

    // The tiles he's on in the passes of a move.
    internal IReadOnlyList<TilePosition> TilesOn(int move) => _tiles[move];
}
