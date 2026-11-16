namespace C64Bot.LodeRunner.Tests;

public class GamePhaseReaderTests
{
    [Theory]
    [InlineData(0, GamePhase.Title)]
    [InlineData(1, GamePhase.Demo)]
    [InlineData(3, GamePhase.Other)]
    [InlineData(5, GamePhase.Other)]
    public void Update_GameModeOtherThanPlay_GivesItsPhase(byte gameMode, GamePhase expected)
    {
        // Arrange
        var reader = new GamePhaseReader();
        var ram = Ram(gameMode: gameMode);

        // Act
        var phase = reader.Update(ram);

        // Assert
        Assert.Equal(expected, phase);
    }

    [Fact]
    public void Update_CounterDroppingBackEachPass_GivesPlaying()
    {
        // Arrange
        var reader = new GamePhaseReader();
        reader.Update(Ram(counter: 4));

        // Act
        var phase = reader.Update(Ram(counter: 3));

        // Assert
        Assert.Equal(GamePhase.Playing, phase);
    }

    [Fact]
    public void Phase_ReadManyTimes_StaysTheLastPhaseWithoutCountingFrames()
    {
        // Arrange
        var reader = new GamePhaseReader();
        reader.Update(Ram(counter: 4));
        reader.Update(Ram(counter: 3));

        // Act
        var phases = Enumerable.Range(0, 10).Select(_ => reader.Phase).ToList();

        // Assert
        Assert.All(phases, phase => Assert.Equal(GamePhase.Playing, phase));
        Assert.True(reader.MainLoopRunning);
    }

    [Fact]
    public void Phase_BeforeTheFirstUpdate_IsOther()
    {
        // Arrange
        var reader = new GamePhaseReader();

        // Act
        var phase = reader.Phase;

        // Assert
        Assert.Equal(GamePhase.Other, phase);
    }

    [Fact]
    public void Update_CounterDroppingBackInTheDemo_SetsMainLoopRunning()
    {
        // Arrange
        var reader = new GamePhaseReader();
        reader.Update(Ram(gameMode: 1, counter: 4));

        // Act
        reader.Update(Ram(gameMode: 1, counter: 3));

        // Assert
        Assert.True(reader.MainLoopRunning);
    }

    [Fact]
    public void Update_FourFramesAfterTheLastPass_StillGivesPlaying()
    {
        // Arrange
        var reader = new GamePhaseReader();
        reader.Update(Ram(counter: 4));
        reader.Update(Ram(counter: 3));
        for (var counter = 4; counter <= 6; counter++)
            reader.Update(Ram(counter: (byte)counter));

        // Act
        var phase = reader.Update(Ram(counter: 7));

        // Assert
        Assert.Equal(GamePhase.Playing, phase);
    }

    [Fact]
    public void Update_FiveFramesAfterTheLastPass_GivesLevelStarting()
    {
        // Arrange
        var reader = new GamePhaseReader();
        reader.Update(Ram(counter: 4));
        reader.Update(Ram(counter: 3));
        for (var counter = 4; counter <= 7; counter++)
            reader.Update(Ram(counter: (byte)counter));

        // Act
        var phase = reader.Update(Ram(counter: 8));

        // Assert
        Assert.Equal(GamePhase.LevelStarting, phase);
    }

    [Fact]
    public void Update_CounterOnlyCountingUp_GivesLevelStarting()
    {
        // Arrange
        var reader = new GamePhaseReader();
        reader.Update(Ram(counter: 3));
        reader.Update(Ram(counter: 4));

        // Act
        var phase = reader.Update(Ram(counter: 5));

        // Assert
        Assert.Equal(GamePhase.LevelStarting, phase);
    }

    [Fact]
    public void Update_CounterWrappingPast255_IsNotAPass()
    {
        // Arrange
        var reader = new GamePhaseReader();
        reader.Update(Ram(counter: 255));

        // Act
        var phase = reader.Update(Ram(counter: 0));

        // Assert
        Assert.Equal(GamePhase.LevelStarting, phase);
    }

    [Fact]
    public void Update_FirstFrame_GivesLevelStartingBecauseNoPassIsSeenYet()
    {
        // Arrange
        var reader = new GamePhaseReader();

        // Act
        var phase = reader.Update(Ram(counter: 3));

        // Assert
        Assert.Equal(GamePhase.LevelStarting, phase);
    }

    [Fact]
    public void Update_MainLoopStoppedWithNoGoldLeft_GivesLevelStarting()
    {
        // Arrange
        var reader = new GamePhaseReader();
        reader.Update(Ram(goldLeft: 0, counter: 4));
        reader.Update(Ram(goldLeft: 0, counter: 3));
        for (var counter = 4; counter <= 7; counter++)
            reader.Update(Ram(goldLeft: 0, counter: (byte)counter));

        // Act
        var phase = reader.Update(Ram(goldLeft: 0, counter: 8));

        // Assert
        Assert.Equal(GamePhase.LevelStarting, phase);
    }

    [Fact]
    public void Update_DeadAfterPlaying_GivesDying()
    {
        // Arrange
        var reader = new GamePhaseReader();
        reader.Update(Ram(counter: 4));
        reader.Update(Ram(counter: 3));

        // Act
        var phase = reader.Update(Ram(alive: 0, counter: 4));

        // Assert
        Assert.Equal(GamePhase.Dying, phase);
    }

    [Fact]
    public void Update_StillDeadAfterTheMainLoopStopped_StaysDying()
    {
        // Arrange
        var reader = new GamePhaseReader();
        reader.Update(Ram(counter: 4));
        reader.Update(Ram(counter: 3));
        for (var counter = 4; counter <= 8; counter++)
            reader.Update(Ram(alive: 0, counter: (byte)counter));

        // Act
        var phase = reader.Update(Ram(alive: 0, counter: 9));

        // Assert
        Assert.Equal(GamePhase.Dying, phase);
    }

    [Fact]
    public void Update_DeadRightAfterTheDemo_GivesLevelStarting()
    {
        // Arrange
        var reader = new GamePhaseReader();
        reader.Update(Ram(gameMode: 1));

        // Act
        var phase = reader.Update(Ram(alive: 0));

        // Assert
        Assert.Equal(GamePhase.LevelStarting, phase);
    }

    [Fact]
    public void Update_DeadWithNoLivesLeft_GivesGameOver()
    {
        // Arrange
        var reader = new GamePhaseReader();

        // Act
        var phase = reader.Update(Ram(alive: 0, lives: 0));

        // Assert
        Assert.Equal(GamePhase.GameOver, phase);
    }

    [Fact]
    public void Update_InterruptVectorNotTheGames_GivesOther()
    {
        // Arrange
        var reader = new GamePhaseReader();
        var ram = Ram(gameMode: 1);
        ram[RamMap.InterruptVector] = 0x31; // the KERNAL's own routine, $EA31, as in the trainer menu
        ram[RamMap.InterruptVector + 1] = 0xEA;

        // Act
        var phase = reader.Update(ram);

        // Assert
        Assert.Equal(GamePhase.Other, phase);
    }

    // RAM with the game running in play mode, the player alive, gold and lives left, and the counter as given.
    private static byte[] Ram(byte gameMode = 2, byte alive = 1, byte goldLeft = 6, byte lives = 5, byte counter = 3)
    {
        var ram = new byte[0x10000];
        ram[RamMap.InterruptVector] = RamMap.GameInterruptRoutine & 0xFF;
        ram[RamMap.InterruptVector + 1] = RamMap.GameInterruptRoutine >> 8;
        ram[RamMap.GameMode] = gameMode;
        ram[RamMap.PlayerAlive] = alive;
        ram[RamMap.GoldLeft] = goldLeft;
        ram[RamMap.Lives] = lives;
        ram[RamMap.InterruptCounter] = counter;
        return ram;
    }
}
