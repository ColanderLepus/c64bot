namespace C64Bot.LodeRunner.Tests;

public class LevelTests
{
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
        ram[RamMap.TileAddress(RamMap.LevelAsBuilt, 3, 2)] = 9;

        // Act
        var level = Level.Read(ram);

        // Assert
        Assert.Equal((Tile)9, level[3, 2]);
    }

    [Fact]
    public void Read_TwoHiddenLadders_ListsThemFromEntryOne()
    {
        // Arrange
        var ram = new byte[0x10000];
        ram[RamMap.HiddenLadderCount] = 2;
        ram[RamMap.HiddenLadderColumns + 1] = 17;
        ram[RamMap.HiddenLadderRows + 1] = 0;
        ram[RamMap.HiddenLadderColumns + 2] = 17;
        ram[RamMap.HiddenLadderRows + 2] = 1;

        // Act
        var level = Level.Read(ram);

        // Assert
        Assert.Equal([new TilePosition(17, 0), new TilePosition(17, 1)], level.HiddenLadders);
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
        Tile[] tiles = [Tile.Empty, Tile.Brick, Tile.Solid, Tile.Ladder, Tile.Rope, Tile.TrapBrick, Tile.Gold, (Tile)6];
        for (var column = 0; column < tiles.Length; column++)
            ram[RamMap.TileAddress(RamMap.LevelAsBuilt, column, 0)] = (byte)tiles[column];

        // Act
        var lines = Level.Read(ram).ToString().Split('\n');

        // Assert
        Assert.Equal(" #@H-T$?" + new string(' ', 20), lines[0]);
        Assert.Equal(Level.Height + 1, lines.Length); // 16 rows, each ending in a line break
    }
}
