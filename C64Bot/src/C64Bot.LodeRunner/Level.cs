using System.Text;

namespace C64Bot.LodeRunner;

/// <summary>
/// A Lode Runner level as it stands: its 28 × 16 tiles, with the gold still there, and where the exit ladders will
/// appear once all gold is taken.
/// </summary>
/// <remarks>
/// Read from the grid as built (<see cref="RamMap.LevelAsBuilt"/>), which is never caught halfway through a move
/// (D14). So it holds no holes, players or guards: a dug brick is still a brick here.
/// </remarks>
public sealed class Level
{
    public const int Width = 28;
    public const int Height = 16;

    // The game keeps at most 45 hidden ladders and blanks out any more; a count beyond that can only be a leftover.
    private const int MaxHiddenLadders = 45;

    private readonly Tile[] _tiles;

    private Level(Tile[] tiles, IReadOnlyList<TilePosition> hiddenLadders)
    {
        _tiles = tiles;
        HiddenLadders = hiddenLadders;
    }

    /// <summary>
    /// Where the exit ladders appear once all gold is taken. They're also in the level from then on, as ladders.
    /// </summary>
    public IReadOnlyList<TilePosition> HiddenLadders { get; }

    /// <exception cref="ArgumentOutOfRangeException">The column or row is outside the level.</exception>
    public Tile this[int column, int row]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(column);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(column, Width);
            ArgumentOutOfRangeException.ThrowIfNegative(row);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(row, Height);
            return _tiles[row * Width + column];
        }
    }

    public Tile this[TilePosition position] => this[position.Column, position.Row];

    /// <summary>
    /// Reads the level from the C64's RAM.
    /// </summary>
    /// <param name="ram">The C64's 64 KB of RAM, for example <c>LibretroCore.SystemRam</c>.</param>
    public static Level Read(ReadOnlySpan<byte> ram)
    {
        var tiles = new Tile[Width * Height];
        for (var row = 0; row < Height; row++)
            for (var column = 0; column < Width; column++)
                tiles[row * Width + column] = (Tile)ram[RamMap.TileAddress(RamMap.LevelAsBuilt, column, row)];

        var count = Math.Min((int)ram[RamMap.HiddenLadderCount], MaxHiddenLadders);
        var hiddenLadders = new TilePosition[count];
        for (var n = 1; n <= count; n++)
            hiddenLadders[n - 1] = new TilePosition(ram[RamMap.HiddenLadderColumns + n], ram[RamMap.HiddenLadderRows + n]);

        return new Level(tiles, hiddenLadders);
    }

    /// <summary>
    /// Draws the level's layout as text, one line per row and one character per tile: a space for empty, <c>#</c>
    /// brick, <c>@</c> solid, <c>H</c> ladder, <c>-</c> rope, <c>T</c> trap brick, <c>$</c> gold, and <c>?</c> for
    /// anything else.
    /// </summary>
    /// <remarks>
    /// The layout only: no player, guards or holes (a dug brick is still drawn as a brick). For the level as it is
    /// on screen, see <see cref="GameState.ToString"/>.
    /// </remarks>
    public override string ToString()
    {
        var text = new StringBuilder();
        for (var row = 0; row < Height; row++)
        {
            for (var column = 0; column < Width; column++)
                text.Append(this[column, row] switch
                {
                    Tile.Empty => ' ',
                    Tile.Brick => '#',
                    Tile.Solid => '@',
                    Tile.Ladder => 'H',
                    Tile.Rope => '-',
                    Tile.TrapBrick => 'T',
                    Tile.Gold => '$',
                    _ => '?',
                });

            text.Append('\n');
        }

        return text.ToString();
    }
}
