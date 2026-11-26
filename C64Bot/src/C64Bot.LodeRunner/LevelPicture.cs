using System.IO.Compression;
using System.Text;

namespace C64Bot.LodeRunner;

/// <summary>
/// A picture of a level, drawn the way the game draws it, made from the level as text, for looking at levels and
/// states outside the game.
/// </summary>
/// <remarks>
/// <para>
/// The text is what <see cref="Level.ToString"/> and <see cref="GameState.ToString"/> give, one line per row from the
/// top: a space for empty, <c>#</c> brick, <c>@</c> solid block, <c>H</c> ladder, <c>-</c> rope, <c>T</c> trap brick,
/// <c>h</c> hidden exit ladder, <c>$</c> gold, <c>G</c> guard, <c>g</c> guard waiting to come back, <c>P</c> player and
/// <c>o</c> open hole. A <c>.</c> marks an empty tile, for example one within reach.
/// </para>
/// <para>
/// Bricks, solid blocks, ladders, ropes, gold, the player and the guards are the game's own pictures, taken from its
/// screen. The rest is drawn so that it can be told apart, which the game doesn't do: a trap brick is a darker brick,
/// a hidden ladder a dark ladder, a guard waiting to come back a dark guard, an open hole the outline of its brick, and
/// a marked tile has a dot in the middle. The columns are numbered 0 to 27 along the top, and the rows lettered A to P
/// down the left, so a tile is named like G8: row G (the seventh, row 6 in code), column 8.
/// </para>
/// </remarks>
public static class LevelPicture
{
    private const int TileWidth = 10;
    private const int TileHeight = 11;
    private const int Left = 14; // room for the row letters
    private const int Top = 8; // room for the column numbers

    private const int Black = 0x000000;
    private const int Brick = 0x814738; // the game's colours
    private const int White = 0xFFFFFF;
    private const int Blue = 0x84BDCC;
    private const int DarkBrick = 0x4A2820;
    private const int Grey = 0x606060;
    private const int Unknown = 0xFF00FF;

    // The game's pictures, 10 by 11 pixels: a for brick red, b for white, c for the guards' blue.
    private static readonly string[] BrickPicture =
    [
        "aaaaaa..aa", "aaaaaa..aa", "aaaaaa..aa", "aaaaaa..aa", "..........",
        "aa..aaaaaa", "aa..aaaaaa", "aa..aaaaaa", "aa..aaaaaa", "aa..aaaaaa", "..........",
    ];

    private static readonly string[] SolidPicture =
    [
        "aaaaaaaaaa", "aaaaaaaaaa", "aaaaaaaaaa", "aaaaaaaaaa", "aaaaaaaaaa",
        "aaaaaaaaaa", "aaaaaaaaaa", "aaaaaaaaaa", "aaaaaaaaaa", "aaaaaaaaaa", "..........",
    ];

    private static readonly string[] LadderPicture =
    [
        "bb....bb..", "bb....bb..", "bbbbbbbb..", "bb....bb..", "bb....bb..",
        "bb....bb..", "bb....bb..", "bbbbbbbb..", "bb....bb..", "bb....bb..", "bb....bb..",
    ];

    private static readonly string[] RopePicture =
    [
        "..........", "bbbbbbbbbb", "..........", "..........", "..........",
        "..........", "..........", "..........", "..........", "..........", "..........",
    ];

    private static readonly string[] GoldPicture =
    [
        "..........", "..........", "..........", "..........", "..........",
        "bbbbbbbb..", "bbaaaabb..", "bbaaaabb..", "bbaaaabb..", "bbbbbbbb..", "..........",
    ];

    private static readonly string[] PlayerPicture =
    [
        "......b...", ".....bbb..", ".....bbb..", "....bbb...", "..bb.bbb..",
        ".bb.bb..bb", "....bb....", "....bbb...", ".bbbb.bb..", "......bb..", "......bb..",
    ];

    private static readonly string[] GuardPicture =
    [
        "...c......", "..ccc.....", "..ccc.....", "...ccc....", "..ccc.cc..",
        "cc..cc.cc.", "....cc....", "...ccc....", "..cc.cccc.", "..cc......", "..cc......",
    ];

    private static readonly string[] HolePicture =
    [
        "aaaaaaaaaa", "a........a", "a........a", "a........a", "a........a",
        "a........a", "a........a", "a........a", "a........a", "aaaaaaaaaa", "..........",
    ];

    private static readonly string[] DotPicture =
    [
        "..........", "..........", "..........", "..........", "....bb....",
        "....bb....", "..........", "..........", "..........", "..........", "..........",
    ];

    // Letters A to P 3 by 5 pixels, for the rows down the left.
    private static readonly string[] Letters =
    [
        ".b.b.bbbbb.bb.b", "bb.b.bbb.b.bbb.", ".bbb..b..b...bb", "bb.b.bb.bb.bbb.", "bbbb..bb.b..bbb",
        "bbbb..bb.b..b..", ".bbb..b.bb.b.bb", "b.bb.bbbbb.bb.b", "bbb.b..b..b.bbb", "..b..b..bb.b.b.",
        "b.bb.bbb.b.bb.b", "b..b..b..b..bbb", "b.bbbbbbbb.bb.b", "bb.b.bb.bb.bb.b", ".b.b.bb.bb.b.b.",
        "bb.b.bbb.b..b..",
    ];

    // Digits 3 by 5 pixels, for the columns along the top.
    private static readonly string[] Digits =
    [
        "bbbb.bb.bb.bbbb", ".b.bb..b..b.bbb", "bbb..bbbbb..bbb", "bbb..bbbb..bbbb", "b.bb.bbbb..b..b",
        "bbbb..bbb..bbbb", "bbbb..bbbb.bbbb", "bbb..b..b..b..b", "bbbb.bbbbb.bbbb", "bbbb.bbbb..bbbb",
    ];

    /// <summary>
    /// Draws the level from its text and gives the picture as the bytes of a PNG file.
    /// </summary>
    /// <param name="text">The level as text, as the remarks say; shorter lines and fewer rows are empty.</param>
    /// <param name="scale">How many pixels on the screen each of the game's pixels becomes, each way.</param>
    public static byte[] Png(string text, int scale = 3)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfLessThan(scale, 1);

        var (width, height) = (Left + Level.Width * TileWidth, Top + Level.Height * TileHeight);
        var pixels = new int[width * height];
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        for (var row = 0; row < Level.Height; row++)
        {
            for (var column = 0; column < Level.Width; column++)
            {
                var character = row < lines.Length && column < lines[row].Length ? lines[row][column] : ' ';
                var (picture, colours) = PictureOf(character);
                Draw(pixels, width, Left + column * TileWidth, Top + row * TileHeight, picture, colours);
            }
        }

        for (var column = 0; column < Level.Width; column++)
            Number(pixels, width, Left + column * TileWidth + 1, 1, column);
        for (var row = 0; row < Level.Height; row++)
            Write(pixels, width, 6, Top + row * TileHeight + 3, Letters[row]);

        return Encode(pixels, width, height, scale);
    }

    /// <summary>
    /// Draws the state as <see cref="GameState.ToString"/> gives it, with its hidden exit ladders and a dot on the empty
    /// tiles marked, and gives the picture as the bytes of a PNG file.
    /// </summary>
    /// <param name="state">The state to draw.</param>
    /// <param name="marked">Tiles to mark with a dot where they're empty, for example those within reach.</param>
    /// <param name="scale">How many pixels on the screen each of the game's pixels becomes, each way.</param>
    public static byte[] Png(GameState state, IReadOnlySet<TilePosition>? marked = null, int scale = 3)
    {
        ArgumentNullException.ThrowIfNull(state);
        var lines = state.ToString().Split('\n').Select(line => line.ToCharArray()).ToArray();
        foreach (var ladder in state.Level.HiddenLadders)
        {
            if (lines[ladder.Row][ladder.Column] == ' ')
                lines[ladder.Row][ladder.Column] = 'h';
        }

        foreach (var tile in marked ?? new HashSet<TilePosition>())
        {
            if (lines[tile.Row][tile.Column] == ' ')
                lines[tile.Row][tile.Column] = '.';
        }

        return Png(string.Join('\n', lines.Select(line => new string(line))), scale);
    }

    private static (string[] Picture, (int A, int B, int C) Colours) PictureOf(char character) => character switch
    {
        ' ' => ([], default),
        '#' => (BrickPicture, (Brick, White, Blue)),
        '@' => (SolidPicture, (Brick, White, Blue)),
        'H' => (LadderPicture, (Brick, White, Blue)),
        '-' => (RopePicture, (Brick, White, Blue)),
        'T' => (BrickPicture, (DarkBrick, White, Blue)),
        'h' => (LadderPicture, (Brick, Grey, Blue)),
        '$' => (GoldPicture, (Brick, White, Blue)),
        'G' => (GuardPicture, (Brick, White, Blue)),
        'g' => (GuardPicture, (Brick, White, Grey)),
        'P' => (PlayerPicture, (Brick, White, Blue)),
        'o' => (HolePicture, (DarkBrick, White, Blue)),
        '.' => (DotPicture, (Brick, Grey, Blue)),
        _ => (SolidPicture, (Unknown, Unknown, Unknown)),
    };

    private static void Draw(int[] pixels, int width, int left, int top, string[] picture, (int A, int B, int C) colours)
    {
        for (var y = 0; y < picture.Length; y++)
        {
            for (var x = 0; x < picture[y].Length; x++)
            {
                pixels[(top + y) * width + left + x] = picture[y][x] switch
                {
                    'a' => colours.A,
                    'b' => colours.B,
                    'c' => colours.C,
                    _ => Black,
                };
            }
        }
    }

    private static void Number(int[] pixels, int width, int left, int top, int number)
    {
        var text = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        for (var i = 0; i < text.Length; i++)
            Write(pixels, width, left + i * 4, top, Digits[text[i] - '0']);
    }

    // A letter or digit, 3 by 5 pixels.
    private static void Write(int[] pixels, int width, int left, int top, string character)
    {
        for (var y = 0; y < 5; y++)
            for (var x = 0; x < 3; x++)
                pixels[(top + y) * width + left + x] = character[y * 3 + x] == 'b' ? Grey : Black;
    }

    // A PNG file: its signature, then the picture's size, its rows of red, green and blue, and the end, each in a
    // chunk with its length and a checksum.
    private static byte[] Encode(int[] pixels, int width, int height, int scale)
    {
        var rows = new MemoryStream();
        for (var y = 0; y < height * scale; y++)
        {
            rows.WriteByte(0); // no filter
            for (var x = 0; x < width * scale; x++)
            {
                var colour = pixels[y / scale * width + x / scale];
                rows.WriteByte((byte)(colour >> 16));
                rows.WriteByte((byte)(colour >> 8));
                rows.WriteByte((byte)colour);
            }
        }

        var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            rows.WriteTo(zlib);

        var header = new byte[13];
        WriteBigEndian(header, 0, width * scale);
        WriteBigEndian(header, 4, height * scale);
        header[8] = 8; // bits per colour
        header[9] = 2; // red, green and blue

        var png = new MemoryStream();
        png.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);
        WriteChunk(png, "IHDR", header);
        WriteChunk(png, "IDAT", compressed.ToArray());
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void WriteChunk(Stream png, string type, byte[] data)
    {
        var length = new byte[4];
        WriteBigEndian(length, 0, data.Length);
        png.Write(length);
        byte[] typed = [.. Encoding.ASCII.GetBytes(type), .. data];
        png.Write(typed);
        var checksum = new byte[4];
        WriteBigEndian(checksum, 0, (int)Crc32(typed));
        png.Write(checksum);
    }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }

        return ~crc;
    }

    private static void WriteBigEndian(byte[] bytes, int at, int value)
    {
        bytes[at] = (byte)(value >> 24);
        bytes[at + 1] = (byte)(value >> 16);
        bytes[at + 2] = (byte)(value >> 8);
        bytes[at + 3] = (byte)value;
    }
}
