namespace C64Bot.LodeRunner.Tests;

public class PlayerTests
{
    [Fact]
    public void Read_PlayerInALevel_GivesHisValues()
    {
        // Arrange
        var ram = Ram(
            column: 14, row: 9, stepX: 4, stepY: 0, sprite: 0x0E, falling: 0x00, digging: 0x01, digStep: 17,
            lastSide: 0x01);

        // Act
        var player = Player.Read(ram);

        // Assert
        Assert.Equal(
            new Player(
                new TilePosition(14, 9), 2, -2, Pose.Dig, Direction.Right, true, Direction.Right, 5, Direction.Right),
            player);
    }

    [Fact]
    public void Read_CentredAndNotFallingOrDigging_GivesZeroOffsetsAndNoFallingOrDigging()
    {
        // Arrange
        var ram = Ram(stepX: 2, stepY: 2, falling: 0x20, digging: 0x00);

        // Act
        var player = Player.Read(ram);

        // Assert
        Assert.NotNull(player);
        Assert.Equal((0, 0), (player.OffsetX, player.OffsetY));
        Assert.False(player.Falling);
        Assert.Null(player.Digging);
    }

    [Theory]
    [InlineData(0x00, Pose.Run, Direction.Left)]
    [InlineData(0x02, Pose.Run, Direction.Left)]
    [InlineData(0x03, Pose.Rope, Direction.Left)]
    [InlineData(0x05, Pose.Rope, Direction.Left)]
    [InlineData(0x06, Pose.Dig, Direction.Left)]
    [InlineData(0x07, Pose.Fall, Direction.Left)]
    [InlineData(0x08, Pose.Run, Direction.Right)]
    [InlineData(0x0A, Pose.Run, Direction.Right)]
    [InlineData(0x0B, Pose.Rope, Direction.Right)]
    [InlineData(0x0D, Pose.Rope, Direction.Right)]
    [InlineData(0x0E, Pose.Dig, Direction.Right)]
    [InlineData(0x0F, Pose.Fall, Direction.Right)]
    [InlineData(0x10, Pose.Ladder, null)]
    [InlineData(0x11, Pose.Ladder, null)]
    public void Read_Sprite_GivesItsPoseAndFacing(byte sprite, Pose pose, Direction? facing)
    {
        // Arrange
        var ram = Ram(sprite: sprite);

        // Act
        var player = Player.Read(ram);

        // Assert
        Assert.NotNull(player);
        Assert.Equal((pose, facing), (player.Pose, player.Facing));
    }

    [Theory]
    [InlineData(0xFF, 1, Direction.Left, 1)]
    [InlineData(0xFF, 12, Direction.Left, 12)]
    [InlineData(0x01, 13, Direction.Right, 1)]
    [InlineData(0x01, 24, Direction.Right, 12)]
    [InlineData(0x00, 24, null, 0)] // the step is left over from the last dig
    public void Read_DigStep_GivesThePassesOfTheDig(byte digging, byte digStep, Direction? side, int passes)
    {
        // Arrange
        var ram = Ram(digging: digging, digStep: digStep);

        // Act
        var player = Player.Read(ram);

        // Assert
        Assert.NotNull(player);
        Assert.Equal((side, passes), (player.Digging, player.DigPasses));
    }

    [Theory]
    [InlineData(0xFF, Direction.Left)]
    [InlineData(0x01, Direction.Right)]
    [InlineData(0x00, Direction.Right)] // no move left or right yet
    public void Read_LastSideOnALadder_GivesTheSideFireDigs(byte lastSide, Direction digSide)
    {
        // Arrange
        var ram = Ram(sprite: 0x10, lastSide: lastSide);

        // Act
        var player = Player.Read(ram);

        // Assert
        Assert.NotNull(player);
        Assert.Null(player.Facing);
        Assert.Equal(digSide, player.DigSide);
    }

    [Theory]
    [InlineData(28, 0, 2, 2, 0x00)]   // column beyond the level, as $FF before a level is set up
    [InlineData(0, 16, 2, 2, 0x00)]   // row beyond the level
    [InlineData(0, 0, 5, 2, 0x00)]    // step beyond 4
    [InlineData(0, 0, 2, 2, 0x12)]    // no such sprite
    public void Read_ValuesNotOfAPlayerInALevel_GivesNull(byte column, byte row, byte stepX, byte stepY, byte sprite)
    {
        // Arrange
        var ram = Ram(column, row, stepX, stepY, sprite);

        // Act
        var player = Player.Read(ram);

        // Assert
        Assert.Null(player);
    }

    private static byte[] Ram(
        byte column = 3, byte row = 4, byte stepX = 2, byte stepY = 2, byte sprite = 0x08, byte falling = 0x20,
        byte digging = 0x00, byte digStep = 0, byte lastSide = 0x01)
    {
        var ram = new byte[0x10000];
        ram[RamMap.PlayerColumn] = column;
        ram[RamMap.PlayerRow] = row;
        ram[RamMap.PlayerStepX] = stepX;
        ram[RamMap.PlayerStepY] = stepY;
        ram[RamMap.PlayerSprite] = sprite;
        ram[RamMap.PlayerFalling] = falling;
        ram[RamMap.PlayerDigging] = digging;
        ram[RamMap.PlayerDigStep] = digStep;
        ram[RamMap.PlayerLastSide] = lastSide;
        return ram;
    }
}
