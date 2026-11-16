namespace C64Bot.LodeRunner.Tests;

public class HoleTests
{
    [Fact]
    public void ReadAll_TwoUsedSlots_GivesTheirHolesInSlotOrder()
    {
        // Arrange
        var ram = new byte[0x10000];
        SetHole(ram, slot: 0, column: 13, row: 15, timer: 180);
        SetHole(ram, slot: 29, column: 5, row: 7, timer: 1);

        // Act
        var holes = Hole.ReadAll(ram);

        // Assert
        Assert.NotNull(holes);
        Assert.Equal([new Hole(new TilePosition(13, 15), 180), new Hole(new TilePosition(5, 7), 1)], holes);
    }

    [Fact]
    public void ReadAll_SlotWithTimerZero_IsNoHoleWhateverItsPosition()
    {
        // Arrange
        var ram = new byte[0x10000];
        SetHole(ram, slot: 3, column: 200, row: 200, timer: 0); // a leftover in a free slot

        // Act
        var holes = Hole.ReadAll(ram);

        // Assert
        Assert.NotNull(holes);
        Assert.Empty(holes);
    }

    [Theory]
    [InlineData(28, 15, 100)]  // column beyond the level
    [InlineData(13, 16, 100)]  // row beyond the level
    [InlineData(13, 15, 181)]  // longer than a hole stays open
    public void ReadAll_HoleThatCantBe_GivesNull(byte column, byte row, byte timer)
    {
        // Arrange
        var ram = new byte[0x10000];
        SetHole(ram, slot: 0, column, row, timer);

        // Act
        var holes = Hole.ReadAll(ram);

        // Assert
        Assert.Null(holes);
    }

    private static void SetHole(byte[] ram, int slot, byte column, byte row, byte timer)
    {
        ram[RamMap.HoleColumns + slot] = column;
        ram[RamMap.HoleRows + slot] = row;
        ram[RamMap.HoleTimers + slot] = timer;
    }
}
