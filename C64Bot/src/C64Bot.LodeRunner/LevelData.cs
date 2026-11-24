using System.Text;
using C64Bot.Libretro;

namespace C64Bot.LodeRunner;

/// <summary>
/// A Lode Runner level as it's stored, before the game sets it up: its number and its 28 × 16 tiles, with where the
/// guards and the player start.
/// </summary>
/// <remarks>
/// <para>
/// A level never changes. The methods that change something give a new level and leave this one as it was, so they
/// can be chained: <c>LevelData.FromDisk(disk, 5).WithoutGuards().With(3, 4, LevelTile.Ladder)</c>. The number
/// stays the same.
/// </para>
/// <para>
/// Any tiles are accepted, also ones that make no playable level, such as none with the player. For a level as it
/// stands during play, see <see cref="Level"/>.
/// </para>
/// </remarks>
public sealed class LevelData
{
    /// <summary>The number of bytes the game stores a level in, two tiles in each.</summary>
    public const int ByteCount = Level.Width * Level.Height / 2;

    // Where the levels are on the disk (see "Levels on the disk" in docs/lode-runner.md).
    private const int FirstTrack = 3;
    private const int LevelsPerTrack = 16;
    private const int FirstByteInSector = 1;

    // The character of each tile in text, by its code: the same ones Level and GameState draw with, and h.
    private const string Characters = " #@H-Th$GP";

    private readonly LevelTile[] _tiles;

    private LevelData(int number, LevelTile[] tiles)
    {
        Number = CheckNumber(number);
        _tiles = tiles;
    }

    /// <summary>
    /// The level's number, 1 to <see cref="GameStarter.MaxLevel"/>. The game shows it, and goes on to the next number
    /// when the level is completed.
    /// </summary>
    public int Number { get; }

    /// <exception cref="ArgumentOutOfRangeException">The column or row is outside the level.</exception>
    public LevelTile this[int column, int row] => _tiles[IndexOf(column, row)];

    /// <exception cref="ArgumentOutOfRangeException">The position is outside the level.</exception>
    public LevelTile this[TilePosition position] => this[position.Column, position.Row];

    /// <summary>
    /// Reads a level from the game's disk.
    /// </summary>
    /// <param name="disk">The Lode Runner disk.</param>
    /// <param name="number">The level's number, 1 to <see cref="GameStarter.MaxLevel"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">There's no such level.</exception>
    public static LevelData FromDisk(DiskImage disk, int number)
    {
        ArgumentNullException.ThrowIfNull(disk);
        var (tracks, sector) = Math.DivRem(CheckNumber(number) - 1, LevelsPerTrack);
        return FromBytes(number, disk.ReadSector(FirstTrack + tracks, sector).Slice(FirstByteInSector, ByteCount));
    }

    /// <summary>
    /// Reads a level from the bytes the game stores it in, for example on the disk or in its level buffer.
    /// </summary>
    /// <param name="number">The level's number, 1 to <see cref="GameStarter.MaxLevel"/>.</param>
    /// <param name="bytes">
    /// <see cref="ByteCount"/> bytes, row by row. Each holds two tiles, the left one in its low four bits.
    /// </param>
    /// <exception cref="ArgumentException">It's not <see cref="ByteCount"/> bytes.</exception>
    /// <exception cref="ArgumentOutOfRangeException">There's no such level number.</exception>
    public static LevelData FromBytes(int number, ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != ByteCount)
            throw new ArgumentException($"A level is {ByteCount} bytes, not {bytes.Length}.", nameof(bytes));

        var tiles = new LevelTile[Level.Width * Level.Height];
        for (var i = 0; i < bytes.Length; i++)
        {
            tiles[2 * i] = (LevelTile)(bytes[i] & 0x0F);
            tiles[2 * i + 1] = (LevelTile)(bytes[i] >> 4);
        }

        return new LevelData(number, tiles);
    }

    /// <summary>
    /// Reads a level from text, one line per row from the top and one character per tile: a space for empty,
    /// <c>#</c> brick, <c>@</c> solid, <c>H</c> ladder, <c>h</c> hidden ladder, <c>-</c> rope, <c>T</c> trap brick,
    /// <c>$</c> gold, <c>G</c> guard and <c>P</c> player.
    /// </summary>
    /// <remarks>
    /// The rest of a short line, and any missing rows at the bottom, are empty.
    /// </remarks>
    /// <param name="number">The number the level gets, 1 to <see cref="GameStarter.MaxLevel"/>.</param>
    /// <param name="text">The level's tiles.</param>
    /// <exception cref="FormatException">The text has another character, or is too big for a level.</exception>
    /// <exception cref="ArgumentOutOfRangeException">There's no such level number.</exception>
    public static LevelData Parse(int number, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        if (lines[^1].Length == 0)
            lines = lines[..^1]; // the text ended with a line break

        if (lines.Length > Level.Height)
            throw new FormatException($"A level has {Level.Height} rows, and the text has {lines.Length} lines.");

        var tiles = new LevelTile[Level.Width * Level.Height];
        for (var row = 0; row < lines.Length; row++)
        {
            if (lines[row].Length > Level.Width)
                throw new FormatException(
                    $"A level has {Level.Width} columns, and line {row + 1} has {lines[row].Length} characters.");

            for (var column = 0; column < lines[row].Length; column++)
            {
                var code = Characters.IndexOf(lines[row][column]);
                if (code < 0)
                    throw new FormatException(
                        $"'{lines[row][column]}' on line {row + 1}, character {column + 1}, isn't a tile.");

                tiles[IndexOf(column, row)] = (LevelTile)code;
            }
        }

        return new LevelData(number, tiles);
    }

    /// <summary>
    /// Gives the level with one tile changed.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The column or row is outside the level, or the tile's code is above 15, which the game can't store.
    /// </exception>
    public LevelData With(int column, int row, LevelTile tile)
    {
        var tiles = (LevelTile[])_tiles.Clone();
        tiles[IndexOf(column, row)] = Storable(tile);
        return new LevelData(Number, tiles);
    }

    /// <summary>
    /// Gives the level with every tile replaced by what the function makes of it.
    /// </summary>
    /// <param name="change">Gets each tile's position and what's there now, and returns what to put there.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The function returned a tile whose code is above 15, which the game can't store.
    /// </exception>
    public LevelData Select(Func<TilePosition, LevelTile, LevelTile> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var tiles = new LevelTile[_tiles.Length];
        for (var i = 0; i < tiles.Length; i++)
            tiles[i] = Storable(change(new TilePosition(i % Level.Width, i / Level.Width), _tiles[i]));

        return new LevelData(Number, tiles);
    }

    /// <summary>
    /// Gives the level with all tiles of one kind replaced by another kind.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The new tile's code is above 15, which the game can't store.
    /// </exception>
    public LevelData Replace(LevelTile from, LevelTile to) => Select((_, tile) => tile == from ? to : tile);

    /// <summary>
    /// Gives the level with empty tiles where the guards start.
    /// </summary>
    public LevelData WithoutGuards() => Replace(LevelTile.Guard, LevelTile.Empty);

    /// <summary>
    /// Gives the <see cref="ByteCount"/> bytes the game stores the level in (see <see cref="FromBytes"/>).
    /// </summary>
    public byte[] ToBytes()
    {
        var bytes = new byte[ByteCount];
        for (var i = 0; i < bytes.Length; i++)
            bytes[i] = (byte)((int)_tiles[2 * i] | (int)_tiles[2 * i + 1] << 4);

        return bytes;
    }

    /// <summary>
    /// Draws the level as the text <see cref="Parse"/> reads, with <c>?</c> for a code that isn't a
    /// <see cref="LevelTile"/> (which <see cref="Parse"/> doesn't accept).
    /// </summary>
    public override string ToString()
    {
        var text = new StringBuilder();
        for (var row = 0; row < Level.Height; row++)
        {
            for (var column = 0; column < Level.Width; column++)
            {
                var code = (int)this[column, row];
                text.Append(code < Characters.Length ? Characters[code] : '?');
            }

            text.Append('\n');
        }

        return text.ToString();
    }

    private static int IndexOf(int column, int row)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(column, Level.Width);
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(row, Level.Height);
        return row * Level.Width + column;
    }

    private static int CheckNumber(int number)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(number, GameStarter.MaxLevel);
        return number;
    }

    private static LevelTile Storable(LevelTile tile)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((int)tile, 0x0F, nameof(tile));
        return tile;
    }
}
