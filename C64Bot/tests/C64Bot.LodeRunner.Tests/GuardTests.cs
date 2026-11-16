namespace C64Bot.LodeRunner.Tests;

public class GuardTests
{
    [Fact]
    public void ReadAll_TwoGuards_GivesBothWithTheirNumbersAndValues()
    {
        // Arrange
        var ram = new byte[0x10000];
        ram[RamMap.GuardCount] = 2;
        SetGuard(ram, 1, column: 5, row: 6, stepX: 2, stepY: 2, sprite: 0x0A);
        SetGuard(ram, 2, column: 23, row: 9, stepX: 0, stepY: 4, sprite: 0x0E);

        // Act
        var guards = Guard.ReadAll(ram);

        // Assert
        Assert.NotNull(guards);
        Assert.Equal(
            [
                new Guard(1, new TilePosition(5, 6), 0, 0, Pose.Rope, Direction.Right, false, false, false),
                new Guard(2, new TilePosition(23, 9), -2, 2, Pose.Ladder, null, false, false, false),
            ],
            guards);
    }

    [Fact]
    public void ReadAll_NoGuards_GivesAnEmptyList()
    {
        // Arrange
        var ram = new byte[0x10000];

        // Act
        var guards = Guard.ReadAll(ram);

        // Assert
        Assert.NotNull(guards);
        Assert.Empty(guards);
    }

    [Theory]
    [InlineData(0x00, 0, false, false, false)]
    [InlineData(0xF0, 0, true, false, false)]   // negative: carries gold
    [InlineData(0xFF, 0, true, false, false)]
    [InlineData(0x26, 0, false, true, false)]   // positive: in a hole
    [InlineData(0x01, 0, false, true, false)]
    [InlineData(0x7F, 20, false, false, true)]  // waiting to respawn
    public void ReadAll_Timers_GiveGoldHoleAndRespawning(
        byte timer, byte respawnTimer, bool carriesGold, bool inHole, bool respawning)
    {
        // Arrange
        var ram = new byte[0x10000];
        ram[RamMap.GuardCount] = 1;
        SetGuard(ram, 1, column: 3, row: 4, stepX: 2, stepY: 2, sprite: 0x00);
        ram[RamMap.GuardTimers + 1] = timer;
        ram[RamMap.GuardRespawnTimers + 1] = respawnTimer;

        // Act
        var guard = Assert.Single(Guard.ReadAll(ram)!);

        // Assert
        Assert.Equal((carriesGold, inHole, respawning), (guard.CarriesGold, guard.InHole, guard.Respawning));
    }

    [Theory]
    [InlineData(0x00, Pose.Run, Direction.Left)]
    [InlineData(0x02, Pose.Run, Direction.Left)]
    [InlineData(0x03, Pose.Rope, Direction.Left)]
    [InlineData(0x05, Pose.Rope, Direction.Left)]
    [InlineData(0x06, Pose.Fall, Direction.Left)]
    [InlineData(0x07, Pose.Run, Direction.Right)]
    [InlineData(0x09, Pose.Run, Direction.Right)]
    [InlineData(0x0A, Pose.Rope, Direction.Right)]
    [InlineData(0x0C, Pose.Rope, Direction.Right)]
    [InlineData(0x0D, Pose.Fall, Direction.Right)]
    [InlineData(0x0E, Pose.Ladder, null)]
    [InlineData(0x0F, Pose.Ladder, null)]
    public void ReadAll_Sprite_GivesItsPoseAndFacing(byte sprite, Pose pose, Direction? facing)
    {
        // Arrange
        var ram = new byte[0x10000];
        ram[RamMap.GuardCount] = 1;
        SetGuard(ram, 1, column: 3, row: 4, stepX: 2, stepY: 2, sprite: sprite);

        // Act
        var guard = Assert.Single(Guard.ReadAll(ram)!);

        // Assert
        Assert.Equal((pose, facing), (guard.Pose, guard.Facing));
    }

    [Theory]
    [InlineData(6, 3, 4, 2, 0x00)]  // more guards than a level has
    [InlineData(1, 28, 4, 2, 0x00)] // column beyond the level
    [InlineData(1, 3, 16, 2, 0x00)] // row beyond the level
    [InlineData(1, 3, 4, 5, 0x00)]  // step beyond 4
    [InlineData(1, 3, 4, 2, 0x10)]  // no such sprite
    public void ReadAll_ValuesNotOfGuardsInALevel_GivesNull(byte count, byte column, byte row, byte stepX, byte sprite)
    {
        // Arrange
        var ram = new byte[0x10000];
        ram[RamMap.GuardCount] = count;
        SetGuard(ram, 1, column, row, stepX, stepY: 2, sprite);

        // Act
        var guards = Guard.ReadAll(ram);

        // Assert
        Assert.Null(guards);
    }

    private static void SetGuard(byte[] ram, int n, byte column, byte row, byte stepX, byte stepY, byte sprite)
    {
        ram[RamMap.GuardColumns + n] = column;
        ram[RamMap.GuardRows + n] = row;
        ram[RamMap.GuardStepsX + n] = stepX;
        ram[RamMap.GuardStepsY + n] = stepY;
        ram[RamMap.GuardSprites + n] = sprite;
    }
}
