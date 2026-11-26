using System.Text;

namespace C64Bot.LodeRunner;

/// <summary>
/// A Lode Runner level: its number and its 28 × 16 tiles.
/// </summary>
/// <remarks>
/// <para>
/// It's a level from the running game (<see cref="Read"/>), one from the game's disk (<see cref="LevelDisk"/>), or a
/// made-up one (<see cref="Parse"/>). One from the running game has its tiles as they are at the moment, without the
/// player, the guards and the holes, which <see cref="GameState"/> has; the others have tiles where the player and
/// the guards start, and where ladders are hidden.
/// </para>
/// <para>
/// A level never changes. The methods that change something give a new level and leave this one as it was, so they
/// can be chained: <c>level.WithoutGuards().With(3, 4, Tile.Ladder)</c>. The number stays the same.
/// </para>
/// </remarks>
public sealed class Level
{
    public const int Width = 28;
    public const int Height = 16;

    /// <summary>The highest level number; the game has 150 levels.</summary>
    public const int MaxNumber = 150;

    /// <summary>The number of bytes the game stores a level in, two tiles in each.</summary>
    public const int ByteCount = Width * Height / 2;

    // The game keeps at most 45 hidden ladders and blanks out any more; a count beyond that can only be a leftover.
    private const int MaxHiddenLadders = 45;

    // The character of each tile in text, by its code.
    private const string Characters = " #@H-Th$GP";

    private readonly Tile[] _tiles;

    // The hidden ladders the running game lists. None for other levels, which have them among their tiles.
    private readonly IReadOnlyList<TilePosition> _listedLadders;

    private Level(int number, Tile[] tiles, IReadOnlyList<TilePosition> listedLadders)
    {
        Number = number;
        _tiles = tiles;

        // A ladder that's among the tiles has appeared, even if the game hasn't crossed it off its list yet.
        _listedLadders = [.. listedLadders.Where(ladder => this[ladder] != Tile.Ladder)];
        HiddenLadders = [.. _listedLadders, .. Positions(tiles, Tile.HiddenLadder)];
    }

    /// <summary>
    /// The level's number, 1 to <see cref="MaxNumber"/>.
    /// </summary>
    /// <remarks>
    /// The number of a level from the running game is the one the game has. It goes up as soon as a level is
    /// completed, so until the next level is set up, it's one ahead of the tiles.
    /// </remarks>
    public int Number { get; }

    /// <summary>
    /// Where the exit ladders are that are still hidden. They appear once all gold is taken.
    /// </summary>
    /// <remarks>
    /// In the running game, the tiles don't show them: they're empty, or have gold that a guard dropped there. When
    /// the ladders appear, one after another, they become tiles and are no longer listed here. The game keeps at most
    /// 45 and drops the rest, so a level on the disk can have more than the same level in the running game.
    /// </remarks>
    public IReadOnlyList<TilePosition> HiddenLadders { get; }

    /// <exception cref="ArgumentOutOfRangeException">The column or row is outside the level.</exception>
    public Tile this[int column, int row] => _tiles[IndexOf(column, row)];

    /// <exception cref="ArgumentOutOfRangeException">The position is outside the level.</exception>
    public Tile this[TilePosition position] => this[position.Column, position.Row];

    /// <summary>
    /// Reads the level as it stands in the running game from the C64's RAM.
    /// </summary>
    /// <remarks>
    /// Read from the grid as built (<see cref="RamMap.LevelAsBuilt"/>), which is never caught halfway through a move
    /// (D14). A dug brick is still a brick in it.
    /// </remarks>
    /// <param name="ram">The C64's 64 KB of RAM, for example <c>LibretroCore.SystemRam</c>.</param>
    public static Level Read(ReadOnlySpan<byte> ram)
    {
        var tiles = new Tile[Width * Height];
        for (var row = 0; row < Height; row++)
            for (var column = 0; column < Width; column++)
                tiles[IndexOf(column, row)] = (Tile)ram[RamMap.TileAddress(RamMap.LevelAsBuilt, column, row)];

        var ladders = new List<TilePosition>();
        var count = Math.Min((int)ram[RamMap.HiddenLadderCount], MaxHiddenLadders);
        for (var n = 1; n <= count; n++)
        {
            // An entry outside the level is one the game has crossed off (column $FF), or a leftover.
            var (column, row) = (ram[RamMap.HiddenLadderColumns + n], ram[RamMap.HiddenLadderRows + n]);
            if (column < Width && row < Height)
                ladders.Add(new TilePosition(column, row));
        }

        return new Level(ram[RamMap.LevelNumber], tiles, ladders);
    }

    /// <summary>
    /// Reads a level from the bytes the game stores it in, for example on the disk or in its level buffer.
    /// </summary>
    /// <param name="number">The level's number, 1 to <see cref="MaxNumber"/>.</param>
    /// <param name="bytes">
    /// <see cref="ByteCount"/> bytes, row by row. Each holds two tiles, the left one in its low four bits.
    /// </param>
    /// <exception cref="ArgumentException">It's not <see cref="ByteCount"/> bytes.</exception>
    /// <exception cref="ArgumentOutOfRangeException">There's no such level number.</exception>
    public static Level FromBytes(int number, ReadOnlySpan<byte> bytes)
    {
        CheckNumber(number);
        if (bytes.Length != ByteCount)
            throw new ArgumentException($"A level is {ByteCount} bytes, not {bytes.Length}.", nameof(bytes));

        var tiles = new Tile[Width * Height];
        for (var i = 0; i < bytes.Length; i++)
        {
            tiles[2 * i] = (Tile)(bytes[i] & 0x0F);
            tiles[2 * i + 1] = (Tile)(bytes[i] >> 4);
        }

        return new Level(number, tiles, []);
    }

    /// <summary>
    /// Reads a made-up level from text, one line per row from the top and one character per tile: a space for empty,
    /// <c>#</c> brick, <c>@</c> solid, <c>H</c> ladder, <c>h</c> hidden ladder, <c>-</c> rope, <c>T</c> trap brick,
    /// <c>$</c> gold, <c>G</c> guard and <c>P</c> player.
    /// </summary>
    /// <remarks>
    /// The rest of a short line, and any missing rows at the bottom, are empty. Any tiles are accepted, also ones that
    /// make no playable level.
    /// </remarks>
    /// <param name="number">The number the level gets, 1 to <see cref="MaxNumber"/>.</param>
    /// <param name="text">The level's tiles.</param>
    /// <exception cref="FormatException">The text has another character, or is too big for a level.</exception>
    /// <exception cref="ArgumentOutOfRangeException">There's no such level number.</exception>
    public static Level Parse(int number, string text)
    {
        CheckNumber(number);
        ArgumentNullException.ThrowIfNull(text);
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        if (lines[^1].Length == 0)
            lines = lines[..^1]; // the text ended with a line break

        if (lines.Length > Height || lines.Any(line => line.Length > Width))
            throw new FormatException($"The text is too big for a level, which has {Height} rows of {Width} tiles.");

        var tiles = new Tile[Width * Height];
        for (var row = 0; row < lines.Length; row++)
            for (var column = 0; column < lines[row].Length; column++)
                tiles[IndexOf(column, row)] = ParseTile(lines[row][column]);

        return new Level(number, tiles, []);
    }

    /// <summary>
    /// Gives the level with one tile changed.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The column or row is outside the level.</exception>
    public Level With(int column, int row, Tile tile)
    {
        var tiles = (Tile[])_tiles.Clone();
        tiles[IndexOf(column, row)] = tile;
        return new Level(Number, tiles, _listedLadders);
    }

    /// <summary>
    /// Gives the level with every tile replaced by what the function makes of it.
    /// </summary>
    /// <param name="change">Gets each tile's position and what's there now, and returns what to put there.</param>
    public Level Select(Func<TilePosition, Tile, Tile> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        return new Level(Number, _tiles.Select((tile, i) => change(PositionOf(i), tile)).ToArray(), _listedLadders);
    }

    /// <summary>
    /// Gives the level with all tiles of one kind replaced by another kind.
    /// </summary>
    public Level Replace(Tile from, Tile to) => Select((_, tile) => tile == from ? to : tile);

    /// <summary>
    /// Gives the level with empty tiles where the guards start.
    /// </summary>
    public Level WithoutGuards() => Replace(Tile.Guard, Tile.Empty);

    /// <summary>
    /// Gives the <see cref="ByteCount"/> bytes the game stores the level in (see <see cref="FromBytes"/>).
    /// </summary>
    /// <exception cref="InvalidOperationException">A tile's code is above 15, which the game can't store.</exception>
    public byte[] ToBytes()
    {
        if (_tiles.Any(tile => (int)tile > 0x0F))
            throw new InvalidOperationException("The game can't store a tile whose code is above 15.");

        var bytes = new byte[ByteCount];
        for (var i = 0; i < bytes.Length; i++)
            bytes[i] = (byte)((int)_tiles[2 * i] | (int)_tiles[2 * i + 1] << 4);

        return bytes;
    }

    /// <summary>
    /// Draws the level as the text <see cref="Parse"/> reads, with <c>?</c> for a code that isn't a
    /// <see cref="Tile"/> (which <see cref="Parse"/> doesn't accept).
    /// </summary>
    /// <remarks>
    /// For a level from the running game with the player, the guards and the holes drawn in, see
    /// <see cref="GameState.ToString"/>.
    /// </remarks>
    public override string ToString()
    {
        var text = new StringBuilder();
        for (var i = 0; i < _tiles.Length; i++)
        {
            var code = (int)_tiles[i];
            text.Append(code < Characters.Length ? Characters[code] : '?');
            if (i % Width == Width - 1)
                text.Append('\n');
        }

        return text.ToString();
    }

    private static Tile ParseTile(char character)
    {
        var code = Characters.IndexOf(character);
        return code >= 0 ? (Tile)code : throw new FormatException($"'{character}' isn't a tile.");
    }

    private static IEnumerable<TilePosition> Positions(Tile[] tiles, Tile tile) =>
        Enumerable.Range(0, tiles.Length).Where(i => tiles[i] == tile).Select(PositionOf);

    private static TilePosition PositionOf(int index) => new(index % Width, index / Width);

    private static int IndexOf(int column, int row)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(column, Width);
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(row, Height);
        return row * Width + column;
    }

    private static void CheckNumber(int number)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(number, MaxNumber);
    }
}
