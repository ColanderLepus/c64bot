namespace C64Bot.LodeRunner.Tests;

public class GameStateTests
{
    [Fact]
    public void TryRead_WholePass_GivesTheLevelAndThePlayer()
    {
        // Arrange
        var ram = Ram(column: 5, row: 10);
        ram[RamMap.TileAddress(RamMap.LevelAsBuilt, 1, 0)] = (byte)Tile.Gold;

        // Act
        var read = GameState.TryRead(ram, out var state);

        // Assert
        Assert.True(read);
        Assert.NotNull(state);
        Assert.Equal(new TilePosition(5, 10), state.Player.Position);
        Assert.Equal(Tile.Gold, state.Level[1, 0]);
    }

    [Theory]
    [InlineData(0x611E, false)] // in the player's routine
    [InlineData(0x612E, false)] // showing the exit ladders
    [InlineData(0x6144, false)] // in the holes' routine
    [InlineData(0x614A, false)] // stopped by an interrupt just before it calls the guards' routine
    [InlineData(0x614C, true)]  // in the guards' routine, or done with it
    [InlineData(0x6155, true)]  // stopped by an interrupt while it waits for the next pass
    [InlineData(0x611C, true)]  // stopped by an interrupt just before it starts the next pass
    [InlineData(0x6098, false)] // setting up the level
    [InlineData(0x60CA, true)]  // waiting for the first move
    [InlineData(0x61C8, true)]  // the player is dead
    [InlineData(0x608E, false)] // before the level is set up
    public void TryRead_MainCodePosition_GivesFalseUntilThePlayerAndTheHolesAreDone(int position, bool expected)
    {
        // Arrange
        var ram = Ram(column: 5, row: 10);
        (ram[RamMap.MainCodePosition], ram[RamMap.MainCodePosition + 1]) = ((byte)position, (byte)(position >> 8));

        // Act
        var read = GameState.TryRead(ram, out _);

        // Assert
        Assert.Equal(expected, read);
    }

    [Fact]
    public void TryRead_NoPlayerInALevel_GivesFalse()
    {
        // Arrange
        var ram = Ram(column: 5, row: 10);
        ram[RamMap.PlayerColumn] = 0xFF; // as before a level is set up

        // Act
        var read = GameState.TryRead(ram, out _);

        // Assert
        Assert.False(read);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    public void TryRead_PlayerAlive_GivesWhetherHeIs(byte value, bool alive)
    {
        // Arrange
        var ram = Ram(column: 5, row: 10);
        ram[RamMap.PlayerAlive] = value;

        // Act
        GameState.TryRead(ram, out var state);

        // Assert
        Assert.Equal(alive, state!.Alive);
    }

    [Theory]
    [InlineData(4, 4, false)]
    [InlineData(5, 4, true)] // the next level's number, and still this level in the buffer
    public void TryRead_LevelNumberAndBuffer_GivesWhetherTheLevelIsCompleted(byte number, byte buffer, bool completed)
    {
        // Arrange
        var ram = Ram(column: 5, row: 10);
        (ram[RamMap.LevelNumberOnDisk], ram[RamMap.LevelInBuffer]) = (number, buffer);

        // Act
        GameState.TryRead(ram, out var state);

        // Assert
        Assert.Equal(completed, state!.Completed);
    }

    [Fact]
    public void ToString_PlayerOnRow9_DrawsPAtHisTileOnTheLayout()
    {
        // Arrange
        var ram = Ram(column: 5, row: 9);
        ram[RamMap.TileAddress(RamMap.LevelAsBuilt, 6, 9)] = (byte)Tile.Ladder;
        GameState.TryRead(ram, out var state);

        // Act
        var lines = state!.ToString().Split('\n');

        // Assert
        Assert.Equal("     PH" + new string(' ', 21), lines[9]);
    }

    [Fact]
    public void ToString_GuardAndRespawningGuard_DrawsGAndLowercaseG()
    {
        // Arrange
        var ram = Ram(column: 0, row: 0);
        ram[RamMap.GuardCount] = 2;
        (ram[RamMap.GuardColumns + 1], ram[RamMap.GuardRows + 1]) = (3, 1);
        (ram[RamMap.GuardColumns + 2], ram[RamMap.GuardRows + 2]) = (7, 1);
        ram[RamMap.GuardStepsX + 1] = ram[RamMap.GuardStepsY + 1] = ram[RamMap.GuardStepsX + 2] = ram[RamMap.GuardStepsY + 2] = 2;
        ram[RamMap.GuardTimers + 2] = 0x7F;
        ram[RamMap.GuardRespawnTimers + 2] = 20;
        GameState.TryRead(ram, out var state);

        // Act
        var lines = state!.ToString().Split('\n');

        // Assert
        Assert.Equal(2, state.Guards.Count);
        Assert.Equal("   G   g" + new string(' ', 20), lines[1]);
    }

    [Fact]
    public void TryRead_GuardValuesNotOfALevel_GivesFalse()
    {
        // Arrange
        var ram = Ram(column: 5, row: 10);
        ram[RamMap.GuardCount] = 6;

        // Act
        var read = GameState.TryRead(ram, out _);

        // Assert
        Assert.False(read);
    }

    [Fact]
    public void TryRead_Counters_GivesLevelLivesScoreAndGold()
    {
        // Arrange
        var ram = Ram(column: 5, row: 10);
        ram[RamMap.LevelNumber] = 150;
        ram[RamMap.Lives] = 6;
        (ram[RamMap.Score], ram[RamMap.Score + 1], ram[RamMap.Score + 2], ram[RamMap.Score + 3]) = (0x50, 0x02, 0x34, 0x12);
        ram[RamMap.GoldLeft] = 7;

        // Act
        GameState.TryRead(ram, out var state);

        // Assert
        Assert.NotNull(state);
        Assert.Equal((150, 6, 12_340_250, 7), (state.Level.Number, state.Lives, state.Score, state.GoldLeft));
    }

    [Fact]
    public void TryRead_GoldCountFF_GivesNoGoldLeft()
    {
        // Arrange
        var ram = Ram(column: 5, row: 10);
        ram[RamMap.GoldLeft] = 0xFF; // all taken, exit ladders shown

        // Act
        GameState.TryRead(ram, out var state);

        // Assert
        Assert.Equal(0, state!.GoldLeft);
    }

    [Fact]
    public void ToString_HoleAndGuardInAnotherHole_DrawsOAndG()
    {
        // Arrange
        var ram = Ram(column: 0, row: 0);
        (ram[RamMap.HoleColumns], ram[RamMap.HoleRows], ram[RamMap.HoleTimers]) = (2, 15, 100);
        (ram[RamMap.HoleColumns + 1], ram[RamMap.HoleRows + 1], ram[RamMap.HoleTimers + 1]) = (6, 15, 50);
        ram[RamMap.GuardCount] = 1;
        (ram[RamMap.GuardColumns + 1], ram[RamMap.GuardRows + 1]) = (6, 15);
        ram[RamMap.GuardStepsX + 1] = ram[RamMap.GuardStepsY + 1] = 2;
        GameState.TryRead(ram, out var state);

        // Act
        var lines = state!.ToString().Split('\n');

        // Assert
        Assert.Equal(2, state.Holes.Count);
        Assert.Equal("  o   G" + new string(' ', 21), lines[15]);
    }

    [Fact]
    public void TryRead_HoleOutsideTheLevel_GivesFalse()
    {
        // Arrange
        var ram = Ram(column: 5, row: 10);
        (ram[RamMap.HoleColumns], ram[RamMap.HoleRows], ram[RamMap.HoleTimers]) = (40, 15, 100);

        // Act
        var read = GameState.TryRead(ram, out _);

        // Assert
        Assert.False(read);
    }

    // RAM with an empty level and the player centred at the given tile, while the game waits for its next pass.
    private static byte[] Ram(byte column, byte row)
    {
        var ram = new byte[0x10000];
        ram[RamMap.PlayerColumn] = column;
        ram[RamMap.PlayerRow] = row;
        ram[RamMap.PlayerStepX] = 2;
        ram[RamMap.PlayerStepY] = 2;
        ram[RamMap.PlayerSprite] = 0x08;
        ram[RamMap.PlayerFalling] = 0x20;
        (ram[RamMap.MainCodePosition], ram[RamMap.MainCodePosition + 1]) = (0x55, 0x61);
        return ram;
    }
}
