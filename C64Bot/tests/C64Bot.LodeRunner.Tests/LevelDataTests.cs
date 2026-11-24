using C64Bot.Libretro;
using C64Bot.Libretro.Tests;

namespace C64Bot.LodeRunner.Tests;

public class LevelDataTests
{
    private const string Text = """
        P  $ G
        ##@HT#
        h--  G
        """;

    [Fact]
    public void Parse_EveryCharacter_GivesItsTile()
    {
        // Arrange
        LevelTile[] expected =
        [
            LevelTile.Empty, LevelTile.Brick, LevelTile.Solid, LevelTile.Ladder, LevelTile.Rope, LevelTile.TrapBrick,
            LevelTile.HiddenLadder, LevelTile.Gold, LevelTile.Guard, LevelTile.Player,
        ];

        // Act
        var level = LevelData.Parse(1, " #@H-Th$GP");

        // Assert
        Assert.Equal(expected, Enumerable.Range(0, 10).Select(column => level[column, 0]));
    }

    [Fact]
    public void Parse_ShortLinesAndMissingRows_FillsUpWithEmptyTiles()
    {
        // Act
        var level = LevelData.Parse(1, "#\n\n##");

        // Assert
        List<TilePosition> bricks = [new(0, 0), new(0, 2), new(1, 2)];
        Assert.Equal(bricks, Positions().Where(position => level[position] != LevelTile.Empty));
    }

    [Theory]
    [InlineData("#\n@")]
    [InlineData("#\r\n@")]
    [InlineData("#\n@\n")]
    [InlineData("#\r\n@\r\n")]
    public void Parse_AnyLineBreaks_GivesTheSameLevel(string text)
    {
        // Act
        var level = LevelData.Parse(1, text);

        // Assert
        Assert.Equal(LevelTile.Brick, level[0, 0]);
        Assert.Equal(LevelTile.Solid, level[0, 1]);
        Assert.Equal(2, Positions().Count(position => level[position] != LevelTile.Empty));
    }

    [Fact]
    public void Parse_FullSize_IsAccepted()
    {
        // Arrange
        var text = string.Concat(Enumerable.Repeat(new string('#', 28) + "\n", 16));

        // Act
        var level = LevelData.Parse(1, text);

        // Assert
        Assert.All(Positions(), position => Assert.Equal(LevelTile.Brick, level[position]));
    }

    [Fact]
    public void Parse_LineTooLong_ThrowsFormatException()
    {
        // Act
        var exception = Record.Exception(() => LevelData.Parse(1, new string('#', 29)));

        // Assert
        Assert.IsType<FormatException>(exception);
    }

    [Fact]
    public void Parse_TooManyLines_ThrowsFormatException()
    {
        // Act
        var exception = Record.Exception(() => LevelData.Parse(1, string.Concat(Enumerable.Repeat("#\n", 17))));

        // Assert
        Assert.IsType<FormatException>(exception);
    }

    [Theory]
    [InlineData("?")]
    [InlineData("x")]
    [InlineData("\t")]
    public void Parse_OtherCharacter_ThrowsFormatException(string text)
    {
        // Act
        var exception = Record.Exception(() => LevelData.Parse(1, text));

        // Assert
        Assert.IsType<FormatException>(exception);
    }

    [Fact]
    public void ToString_ParsedAgain_GivesTheSameLevel()
    {
        // Arrange
        var level = LevelData.Parse(1, Text);

        // Act
        var again = LevelData.Parse(1, level.ToString());

        // Assert
        Assert.Equal(level.ToBytes(), again.ToBytes());
    }

    [Fact]
    public void ToString_UnknownCode_DrawsAQuestionMark()
    {
        // Arrange
        var bytes = new byte[LevelData.ByteCount];
        bytes[0] = 0x0A;

        // Act
        var text = LevelData.FromBytes(1, bytes).ToString();

        // Assert
        Assert.Equal('?', text[0]);
        Assert.Equal(' ', text[1]);
    }

    [Fact]
    public void FromBytes_TakesTheLeftTileFromTheLowBitsRowByRow()
    {
        // Arrange
        var bytes = new byte[LevelData.ByteCount];
        bytes[0] = 0x21;  // the first two tiles of row 0
        bytes[14] = 0x93; // the first two tiles of row 1

        // Act
        var level = LevelData.FromBytes(1, bytes);

        // Assert
        Assert.Equal(LevelTile.Brick, level[0, 0]);
        Assert.Equal(LevelTile.Solid, level[1, 0]);
        Assert.Equal(LevelTile.Ladder, level[0, 1]);
        Assert.Equal(LevelTile.Player, level[1, 1]);
    }

    [Theory]
    [InlineData(LevelData.ByteCount - 1)]
    [InlineData(LevelData.ByteCount + 1)]
    public void FromBytes_WrongNumberOfBytes_ThrowsArgumentException(int count)
    {
        // Act
        var exception = Record.Exception(() => LevelData.FromBytes(1, new byte[count]));

        // Assert
        Assert.IsType<ArgumentException>(exception);
    }

    [Fact]
    public void ToBytes_EveryPairOfCodes_GivesBackTheSameBytes()
    {
        // Arrange: the 224 bytes from 0 and the last 224, which together are all 256 values
        var low = Enumerable.Range(0, LevelData.ByteCount).Select(i => (byte)i).ToArray();
        var high = Enumerable.Range(256 - LevelData.ByteCount, LevelData.ByteCount).Select(i => (byte)i).ToArray();

        // Act
        var lowAgain = LevelData.FromBytes(1, low).ToBytes();
        var highAgain = LevelData.FromBytes(1, high).ToBytes();

        // Assert
        Assert.Equal(low, lowAgain);
        Assert.Equal(high, highAgain);
    }

    [Fact]
    public void With_ChangesOnlyThatTileInANewLevel()
    {
        // Arrange
        var level = LevelData.Parse(1, Text);
        var before = level.ToBytes();

        // Act
        var changed = level.With(column: 3, row: 4, LevelTile.Ladder);

        // Assert
        Assert.Equal(LevelTile.Ladder, changed[3, 4]);
        Assert.Equal([new TilePosition(3, 4)], Positions().Where(position => changed[position] != level[position]));
        Assert.Equal(before, level.ToBytes());
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(28, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 16)]
    public void With_OutsideTheLevel_ThrowsArgumentOutOfRangeException(int column, int row)
    {
        // Arrange
        var level = LevelData.Parse(1, Text);

        // Act
        var exception = Record.Exception(() => level.With(column, row, LevelTile.Brick));

        // Assert
        Assert.IsType<ArgumentOutOfRangeException>(exception);
    }

    [Fact]
    public void With_CodeThatCannotBeStored_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var level = LevelData.Parse(1, Text);

        // Act
        var exception = Record.Exception(() => level.With(0, 0, (LevelTile)16));

        // Assert
        Assert.IsType<ArgumentOutOfRangeException>(exception);
    }

    [Fact]
    public void Select_GetsEveryPositionWithItsTile()
    {
        // Arrange
        var level = LevelData.Parse(1, Text);
        var seen = new List<(TilePosition Position, LevelTile Tile)>();

        // Act
        level.Select((position, tile) =>
        {
            seen.Add((position, tile));
            return tile;
        });

        // Assert
        Assert.Equal(Positions().Select(position => (position, level[position])), seen);
    }

    [Fact]
    public void Select_PutsWhatTheFunctionReturnsInANewLevel()
    {
        // Arrange
        var level = LevelData.Parse(1, Text);
        var before = level.ToBytes();

        // Act
        var changed = level.Select((position, tile) => position.Row == 1 ? LevelTile.Solid : tile);

        // Assert
        Assert.All(Positions(), position =>
            Assert.Equal(position.Row == 1 ? LevelTile.Solid : level[position], changed[position]));
        Assert.Equal(before, level.ToBytes());
    }

    [Fact]
    public void Replace_ChangesEveryTileOfThatKindAndNoOthers()
    {
        // Arrange
        var level = LevelData.Parse(1, Text);

        // Act
        var changed = level.Replace(LevelTile.Brick, LevelTile.Gold);

        // Assert
        Assert.All(Positions(), position =>
            Assert.Equal(level[position] == LevelTile.Brick ? LevelTile.Gold : level[position], changed[position]));
    }

    [Fact]
    public void WithoutGuards_LeavesEmptyTilesAndEverythingElse()
    {
        // Arrange
        var level = LevelData.Parse(1, Text);

        // Act
        var changed = level.WithoutGuards();

        // Assert
        Assert.Equal(LevelData.Parse(1, "P  $\n##@HT#\nh--").ToBytes(), changed.ToBytes());
    }

    [Fact]
    public void EveryChange_KeepsTheNumber()
    {
        // Arrange
        var level = LevelData.Parse(42, Text);

        // Act
        LevelData[] changed =
        [
            level.With(0, 0, LevelTile.Brick), level.Select((_, tile) => tile),
            level.Replace(LevelTile.Brick, LevelTile.Solid), level.WithoutGuards(),
        ];

        // Assert
        Assert.All(changed, one => Assert.Equal(42, one.Number));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(151)]
    public void Parse_NoSuchNumber_ThrowsArgumentOutOfRangeException(int number)
    {
        // Act
        var exception = Record.Exception(() => LevelData.Parse(number, Text));

        // Assert
        Assert.IsType<ArgumentOutOfRangeException>(exception);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(151)]
    public void FromBytes_NoSuchNumber_ThrowsArgumentOutOfRangeException(int number)
    {
        // Act
        var exception = Record.Exception(() => LevelData.FromBytes(number, new byte[LevelData.ByteCount]));

        // Assert
        Assert.IsType<ArgumentOutOfRangeException>(exception);
    }

    [CoreFact]
    public void FromDisk_EveryLevel_HasItsNumber()
    {
        // Arrange
        var disk = DiskImage.Load(CoreFiles.DiskPath);

        // Act
        var numbers = AllLevels().Select(level => LevelData.FromDisk(disk, level).Number);

        // Assert
        Assert.Equal(AllLevels(), numbers);
    }

    [Fact]
    public void Chained_AllChangesAreInTheResult()
    {
        // Arrange
        var level = LevelData.Parse(1, Text);

        // Act
        var changed = level.WithoutGuards().Replace(LevelTile.TrapBrick, LevelTile.Brick).With(5, 0, LevelTile.Gold);

        // Assert
        Assert.Equal(LevelData.Parse(1, "P  $ $\n##@H##\nh--").ToBytes(), changed.ToBytes());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(151)]
    public void FromDisk_NoSuchLevel_ThrowsArgumentOutOfRangeException(int level)
    {
        // Arrange
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, new byte[174_848]);

        try
        {
            var disk = DiskImage.Load(path);

            // Act
            var exception = Record.Exception(() => LevelData.FromDisk(disk, level));

            // Assert
            Assert.IsType<ArgumentOutOfRangeException>(exception);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [CoreFact]
    public void FromDisk_EveryLevel_GivesBackTheBytesOnTheDisk()
    {
        // Arrange
        var disk = DiskImage.Load(CoreFiles.DiskPath);
        var onDisk = AllLevels().Select(level =>
            disk.ReadSector((level - 1) / 16 + 3, (level - 1) % 16).Slice(1, 224).ToArray());

        // Act
        var bytes = AllLevels().Select(level => LevelData.FromDisk(disk, level).ToBytes());

        // Assert
        Assert.Equal(onDisk, bytes);
    }

    // The numbers are the ones in docs/lode-runner.md ("Guards per level").
    [CoreFact]
    public void FromDisk_EveryLevel_HasTheGuardsItShould()
    {
        // Arrange
        var disk = DiskImage.Load(CoreFiles.DiskPath);

        // Act
        var levelsByGuards = AllLevels()
            .Select(level => LevelData.FromDisk(disk, level))
            .CountBy(level => Math.Min(5, Positions().Count(position => level[position] == LevelTile.Guard)))
            .OrderBy(pair => pair.Key)
            .Select(pair => (pair.Key, pair.Value));

        // Assert
        Assert.Equal([(2, 16), (3, 70), (4, 47), (5, 17)], levelsByGuards);
    }

    private static IEnumerable<int> AllLevels() => Enumerable.Range(1, GameStarter.MaxLevel);

    private static IEnumerable<TilePosition> Positions() =>
        from row in Enumerable.Range(0, Level.Height)
        from column in Enumerable.Range(0, Level.Width)
        select new TilePosition(column, row);
}
