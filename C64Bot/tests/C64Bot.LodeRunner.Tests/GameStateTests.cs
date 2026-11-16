namespace C64Bot.LodeRunner.Tests;

public class GameStateTests
{
    [Fact]
    public void TryRead_PlayerTileWhereThePlayerIs_GivesTheLevelAndThePlayer()
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

    [Fact]
    public void TryRead_NoPlayerTileWhereThePlayerIs_GivesFalse()
    {
        // Arrange
        var ram = Ram(column: 5, row: 10);
        ram[RamMap.TileAddress(RamMap.LevelAsPlayed, 5, 10)] = 0; // his move caught halfway

        // Act
        var read = GameState.TryRead(ram, out var state);

        // Assert
        Assert.False(read);
        Assert.Null(state);
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
        Assert.Equal((150, 6, 12_340_250, 7), (state.LevelNumber, state.Lives, state.Score, state.GoldLeft));
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

    // RAM with an empty level and the player centred at the given tile, marked in the grid as played.
    private static byte[] Ram(byte column, byte row)
    {
        var ram = new byte[0x10000];
        ram[RamMap.PlayerColumn] = column;
        ram[RamMap.PlayerRow] = row;
        ram[RamMap.PlayerStepX] = 2;
        ram[RamMap.PlayerStepY] = 2;
        ram[RamMap.PlayerSprite] = 0x08;
        ram[RamMap.PlayerFalling] = 0x20;
        ram[RamMap.TileAddress(RamMap.LevelAsPlayed, column, row)] = 9;
        return ram;
    }
}
