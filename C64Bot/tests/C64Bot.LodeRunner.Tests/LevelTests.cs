namespace C64Bot.LodeRunner.Tests;

public class LevelTests
{
    private const string Text = """
        P  $ G
        ##@HT#
        h--  G
        """;

    [Fact]
    public void Read_TilesEitherSideOfTheGapAfterRow8_AreAtTheirPlaces()
    {
        // Arrange
        var ram = new byte[0x10000];
        ram[0x0A00 + 28 * 8 + 27] = (byte)Tile.Gold;     // row 8, last column: $0AFB
        ram[0x0A00 + 28 * 9 + 4] = (byte)Tile.Ladder;    // row 9, first column: $0B00, after the 4-byte gap
        ram[0x0A00 + 28 * 15 + 4 + 27] = (byte)Tile.Solid; // row 15, last column: $0BC3

        // Act
        var level = Level.Read(ram);

        // Assert
        Assert.Equal(Tile.Gold, level[27, 8]);
        Assert.Equal(Tile.Ladder, level[0, 9]);
        Assert.Equal(Tile.Solid, level[27, 15]);
        Assert.Equal(Tile.Empty, level[1, 9]);
    }

    [Fact]
    public void Read_ValueThatIsNoTile_IsKeptAsItIs()
    {
        // Arrange
        var ram = new byte[0x10000];
        ram[RamMap.TileAddress(RamMap.LevelAsBuilt, 3, 2)] = 10;

        // Act
        var level = Level.Read(ram);

        // Assert
        Assert.Equal((Tile)10, level[3, 2]);
    }

    [Fact]
    public void Read_TwoHiddenLadders_ListsThemFromEntryOne()
    {
        // Arrange
        var ram = RamWithHiddenLadders(new(17, 0), new(17, 1));

        // Act
        var level = Level.Read(ram);

        // Assert
        Assert.Equal([new TilePosition(17, 0), new TilePosition(17, 1)], level.HiddenLadders);
        Assert.Equal(Tile.Empty, level[17, 0]);
    }

    [Fact]
    public void Read_GoldOnAHiddenLadder_GivesBoth()
    {
        // Arrange: a guard dropped it there
        var ram = RamWithHiddenLadders(new TilePosition(17, 0));
        ram[RamMap.TileAddress(RamMap.LevelAsBuilt, 17, 0)] = (byte)Tile.Gold;

        // Act
        var level = Level.Read(ram);

        // Assert
        Assert.Equal(Tile.Gold, level[17, 0]);
        Assert.Equal([new TilePosition(17, 0)], level.HiddenLadders);
    }

    [Theory]
    [InlineData(17)]   // not crossed off the list yet
    [InlineData(0xFF)] // crossed off
    public void Read_LadderHasAppeared_ListsOnlyTheOneStillHidden(byte column)
    {
        // Arrange
        var ram = RamWithHiddenLadders(new(17, 0), new(17, 1));
        ram[RamMap.TileAddress(RamMap.LevelAsBuilt, 17, 1)] = (byte)Tile.Ladder;
        ram[RamMap.HiddenLadderColumns + 2] = column;

        // Act
        var level = Level.Read(ram);

        // Assert
        Assert.Equal(Tile.Ladder, level[17, 1]);
        Assert.Equal([new TilePosition(17, 0)], level.HiddenLadders);
    }

    [Fact]
    public void Read_CountBeyondTheMost_ListsOnlyTheMostThereCanBe()
    {
        // Arrange
        var ram = new byte[0x10000];
        ram[RamMap.HiddenLadderCount] = 46; // a leftover

        // Act
        var level = Level.Read(ram);

        // Assert
        Assert.Equal(45, level.HiddenLadders.Count);
    }

    [Fact]
    public void HiddenLadders_LevelFromText_AreItsHiddenLadderTiles()
    {
        // Act
        var level = Level.Parse(1, " h\nh H");

        // Assert
        Assert.Equal([new TilePosition(1, 0), new TilePosition(0, 1)], level.HiddenLadders);
    }

    [Fact]
    public void HiddenLadders_TileReplaced_FollowTheTiles()
    {
        // Arrange
        var level = Level.Parse(1, " h\nh H");

        // Act
        var changed = level.With(1, 0, Tile.Empty).With(5, 5, Tile.HiddenLadder);

        // Assert
        Assert.Equal([new TilePosition(0, 1), new TilePosition(5, 5)], changed.HiddenLadders);
    }

    [Fact]
    public void HiddenLadders_ChangedLevelFromTheRunningGame_KeepsTheGamesList()
    {
        // Arrange
        var level = Level.Read(RamWithHiddenLadders(new TilePosition(17, 0)));

        // Act
        var changed = level.With(17, 0, Tile.Gold).Replace(Tile.Brick, Tile.Solid);

        // Assert
        Assert.Equal([new TilePosition(17, 0)], changed.HiddenLadders);
    }

    [Fact]
    public void HiddenLadders_LadderPutWhereOneIsHidden_NoLongerHasIt()
    {
        // Arrange
        var level = Level.Read(RamWithHiddenLadders(new(17, 0), new(17, 1)));

        // Act
        var changed = level.With(17, 1, Tile.Ladder);

        // Assert
        Assert.Equal([new TilePosition(17, 0)], changed.HiddenLadders);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(250)]
    public void Read_AnyNumber_GivesTheOneTheGameHas(byte number)
    {
        // Arrange
        var ram = new byte[0x10000];
        ram[RamMap.LevelNumber] = number;

        // Act
        var level = Level.Read(ram);

        // Assert
        Assert.Equal(number, level.Number);
    }

    [Fact]
    public void Indexer_ColumnOutsideTheLevel_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var level = Level.Read(new byte[0x10000]);

        // Act
        var exception = Record.Exception(() => level[28, 0]);

        // Assert
        Assert.IsType<ArgumentOutOfRangeException>(exception);
    }

    [Fact]
    public void ToString_EachKindOfTile_DrawsItsCharacter()
    {
        // Arrange
        var ram = new byte[0x10000];
        Tile[] tiles = [Tile.Empty, Tile.Brick, Tile.Solid, Tile.Ladder, Tile.Rope, Tile.TrapBrick, Tile.Gold, (Tile)10];
        for (var column = 0; column < tiles.Length; column++)
            ram[RamMap.TileAddress(RamMap.LevelAsBuilt, column, 0)] = (byte)tiles[column];

        // Act
        var lines = Level.Read(ram).ToString().Split('\n');

        // Assert
        Assert.Equal(" #@H-T$?" + new string(' ', 20), lines[0]);
        Assert.Equal(Level.Height + 1, lines.Length); // 16 rows, each ending in a line break
    }

    [Fact]
    public void Parse_EveryCharacter_GivesItsTile()
    {
        // Arrange
        Tile[] expected =
        [
            Tile.Empty, Tile.Brick, Tile.Solid, Tile.Ladder, Tile.Rope, Tile.TrapBrick,
            Tile.HiddenLadder, Tile.Gold, Tile.Guard, Tile.Player,
        ];

        // Act
        var level = Level.Parse(1, " #@H-Th$GP");

        // Assert
        Assert.Equal(expected, Enumerable.Range(0, 10).Select(column => level[column, 0]));
    }

    [Fact]
    public void Parse_ShortLinesAndMissingRows_FillsUpWithEmptyTiles()
    {
        // Act
        var level = Level.Parse(1, "#\n\n##");

        // Assert
        List<TilePosition> bricks = [new(0, 0), new(0, 2), new(1, 2)];
        Assert.Equal(bricks, Positions().Where(position => level[position] != Tile.Empty));
    }

    [Theory]
    [InlineData("#\n@")]
    [InlineData("#\r\n@")]
    [InlineData("#\n@\n")]
    [InlineData("#\r\n@\r\n")]
    public void Parse_AnyLineBreaks_GivesTheSameLevel(string text)
    {
        // Act
        var level = Level.Parse(1, text);

        // Assert
        Assert.Equal(Tile.Brick, level[0, 0]);
        Assert.Equal(Tile.Solid, level[0, 1]);
        Assert.Equal(2, Positions().Count(position => level[position] != Tile.Empty));
    }

    [Fact]
    public void Parse_FullSize_IsAccepted()
    {
        // Arrange
        var text = string.Concat(Enumerable.Repeat(new string('#', 28) + "\n", 16));

        // Act
        var level = Level.Parse(1, text);

        // Assert
        Assert.All(Positions(), position => Assert.Equal(Tile.Brick, level[position]));
    }

    [Fact]
    public void Parse_LineTooLong_ThrowsFormatException()
    {
        // Act
        var exception = Record.Exception(() => Level.Parse(1, new string('#', 29)));

        // Assert
        Assert.IsType<FormatException>(exception);
    }

    [Fact]
    public void Parse_TooManyLines_ThrowsFormatException()
    {
        // Act
        var exception = Record.Exception(() => Level.Parse(1, string.Concat(Enumerable.Repeat("#\n", 17))));

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
        var exception = Record.Exception(() => Level.Parse(1, text));

        // Assert
        Assert.IsType<FormatException>(exception);
    }

    [Fact]
    public void ToString_ParsedAgain_GivesTheSameLevel()
    {
        // Arrange
        var level = Level.Parse(1, Text);

        // Act
        var again = Level.Parse(1, level.ToString());

        // Assert
        Assert.Equal(level.ToBytes(), again.ToBytes());
    }

    [Fact]
    public void ToString_UnknownCode_DrawsAQuestionMark()
    {
        // Arrange
        var bytes = new byte[Level.ByteCount];
        bytes[0] = 0x0A;

        // Act
        var text = Level.FromBytes(1, bytes).ToString();

        // Assert
        Assert.Equal('?', text[0]);
        Assert.Equal(' ', text[1]);
    }

    [Fact]
    public void FromBytes_TakesTheLeftTileFromTheLowBitsRowByRow()
    {
        // Arrange
        var bytes = new byte[Level.ByteCount];
        bytes[0] = 0x21;  // the first two tiles of row 0
        bytes[14] = 0x93; // the first two tiles of row 1

        // Act
        var level = Level.FromBytes(1, bytes);

        // Assert
        Assert.Equal(Tile.Brick, level[0, 0]);
        Assert.Equal(Tile.Solid, level[1, 0]);
        Assert.Equal(Tile.Ladder, level[0, 1]);
        Assert.Equal(Tile.Player, level[1, 1]);
    }

    [Theory]
    [InlineData(Level.ByteCount - 1)]
    [InlineData(Level.ByteCount + 1)]
    public void FromBytes_WrongNumberOfBytes_ThrowsArgumentException(int count)
    {
        // Act
        var exception = Record.Exception(() => Level.FromBytes(1, new byte[count]));

        // Assert
        Assert.IsType<ArgumentException>(exception);
    }

    [Fact]
    public void ToBytes_EveryPairOfCodes_GivesBackTheSameBytes()
    {
        // Arrange: the 224 bytes from 0 and the last 224, which together are all 256 values
        var low = Enumerable.Range(0, Level.ByteCount).Select(i => (byte)i).ToArray();
        var high = Enumerable.Range(256 - Level.ByteCount, Level.ByteCount).Select(i => (byte)i).ToArray();

        // Act
        var lowAgain = Level.FromBytes(1, low).ToBytes();
        var highAgain = Level.FromBytes(1, high).ToBytes();

        // Assert
        Assert.Equal(low, lowAgain);
        Assert.Equal(high, highAgain);
    }

    [Fact]
    public void With_ChangesOnlyThatTileInANewLevel()
    {
        // Arrange
        var level = Level.Parse(1, Text);
        var before = level.ToBytes();

        // Act
        var changed = level.With(column: 3, row: 4, Tile.Ladder);

        // Assert
        Assert.Equal(Tile.Ladder, changed[3, 4]);
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
        var level = Level.Parse(1, Text);

        // Act
        var exception = Record.Exception(() => level.With(column, row, Tile.Brick));

        // Assert
        Assert.IsType<ArgumentOutOfRangeException>(exception);
    }

    [Fact]
    public void ToBytes_CodeThatCannotBeStored_ThrowsInvalidOperationException()
    {
        // Arrange
        var level = Level.Parse(1, Text).With(0, 0, (Tile)16);

        // Act
        var exception = Record.Exception(() => level.ToBytes());

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public void Select_GetsEveryPositionWithItsTile()
    {
        // Arrange
        var level = Level.Parse(1, Text);
        var seen = new List<(TilePosition Position, Tile Tile)>();

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
        var level = Level.Parse(1, Text);
        var before = level.ToBytes();

        // Act
        var changed = level.Select((position, tile) => position.Row == 1 ? Tile.Solid : tile);

        // Assert
        Assert.All(Positions(), position =>
            Assert.Equal(position.Row == 1 ? Tile.Solid : level[position], changed[position]));
        Assert.Equal(before, level.ToBytes());
    }

    [Fact]
    public void Replace_ChangesEveryTileOfThatKindAndNoOthers()
    {
        // Arrange
        var level = Level.Parse(1, Text);

        // Act
        var changed = level.Replace(Tile.Brick, Tile.Gold);

        // Assert
        Assert.All(Positions(), position =>
            Assert.Equal(level[position] == Tile.Brick ? Tile.Gold : level[position], changed[position]));
    }

    [Fact]
    public void WithoutGuards_LeavesEmptyTilesAndEverythingElse()
    {
        // Arrange
        var level = Level.Parse(1, Text);

        // Act
        var changed = level.WithoutGuards();

        // Assert
        Assert.Equal(Level.Parse(1, "P  $\n##@HT#\nh--").ToBytes(), changed.ToBytes());
    }

    [Fact]
    public void EveryChange_KeepsTheNumber()
    {
        // Arrange
        var level = Level.Parse(42, Text);

        // Act
        Level[] changed =
        [
            level.With(0, 0, Tile.Brick), level.Select((_, tile) => tile),
            level.Replace(Tile.Brick, Tile.Solid), level.WithoutGuards(),
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
        var exception = Record.Exception(() => Level.Parse(number, Text));

        // Assert
        Assert.IsType<ArgumentOutOfRangeException>(exception);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(151)]
    public void FromBytes_NoSuchNumber_ThrowsArgumentOutOfRangeException(int number)
    {
        // Act
        var exception = Record.Exception(() => Level.FromBytes(number, new byte[Level.ByteCount]));

        // Assert
        Assert.IsType<ArgumentOutOfRangeException>(exception);
    }

    [Fact]
    public void Chained_AllChangesAreInTheResult()
    {
        // Arrange
        var level = Level.Parse(1, Text);

        // Act
        var changed = level.WithoutGuards().Replace(Tile.TrapBrick, Tile.Brick).With(5, 0, Tile.Gold);

        // Assert
        Assert.Equal(Level.Parse(1, "P  $ $\n##@H##\nh--").ToBytes(), changed.ToBytes());
    }

    private static byte[] RamWithHiddenLadders(params TilePosition[] positions)
    {
        var ram = new byte[0x10000];
        ram[RamMap.HiddenLadderCount] = (byte)positions.Length;
        for (var n = 1; n <= positions.Length; n++)
        {
            ram[RamMap.HiddenLadderColumns + n] = (byte)positions[n - 1].Column;
            ram[RamMap.HiddenLadderRows + n] = (byte)positions[n - 1].Row;
        }

        return ram;
    }

    private static IEnumerable<TilePosition> Positions() =>
        from row in Enumerable.Range(0, Level.Height)
        from column in Enumerable.Range(0, Level.Width)
        select new TilePosition(column, row);
}
