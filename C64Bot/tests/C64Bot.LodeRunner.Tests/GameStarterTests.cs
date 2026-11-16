using C64Bot.Libretro;
using C64Bot.Libretro.Tests;

namespace C64Bot.LodeRunner.Tests;

[Collection(LodeRunnerCollection.Name)]
public class GameStarterTests(LodeRunnerReadyToStart lodeRunner)
{
    // Setting up a level takes about 650 frames after StartLevel; these leave room.
    private const int FramesToSetUpALevel = 900;
    private const int MaxFramesUntilPlaying = 1500;

    private LibretroCore Core => lodeRunner.Core;

    [CoreFact]
    public void StartLevel_Level5AfterLevel1_SetsUpADifferentLevelNumberedFive()
    {
        // Arrange
        lodeRunner.Starter.StartLevel(1);
        RunFrames(FramesToSetUpALevel);
        var level1 = LevelAsBuilt();

        // Act
        lodeRunner.Starter.StartLevel(5);
        RunFrames(FramesToSetUpALevel);

        // Assert
        Assert.Equal(5, Core.SystemRam[RamMap.LevelNumber]);
        Assert.Equal(4, Core.SystemRam[RamMap.LevelNumberOnDisk]);
        Assert.NotEqual(level1, LevelAsBuilt());
    }

    [CoreFact]
    public void StartLevel_WithoutAMove_KeepsWaitingForTheFirstMove()
    {
        // Arrange
        lodeRunner.Starter.StartLevel(1);

        // Act
        var phase = RunFramesWithNoInput(MaxFramesUntilPlaying);

        // Assert
        Assert.Equal(GamePhase.LevelStarting, phase);
    }

    [CoreFact]
    public void StartLevel_SameMoveHeldBeforeAndAfter_StartsTheLevel()
    {
        // Arrange: level 1 running, started by holding left, and left still held
        lodeRunner.Starter.StartLevel(1);
        var firstTry = HoldUntilPlaying(RetroPadButton.Left);

        // Act: a new try at the same level, holding left again
        lodeRunner.Starter.StartLevel(1);
        var secondTry = HoldUntilPlaying(RetroPadButton.Left);

        // Assert
        Assert.Equal(GamePhase.Playing, firstTry);
        Assert.Equal(GamePhase.Playing, secondTry);
    }

    [CoreFact]
    public void LoadState_StateSavedWhileWalking_ComesBackWithThePlayerStandingStill()
    {
        // Arrange: level 1 running, the player walking left when the state is saved; then let go, so the core
        // last saw no buttons before the load
        lodeRunner.Starter.StartLevel(1);
        HoldUntilPlaying(RetroPadButton.Left);
        RunFrames(10);
        var walking = Core.SaveState();
        Core.ReleaseButton(RetroPadButton.Left);
        RunFrames(5);

        // Act
        Core.LoadState(walking);
        RunFrames(10); // a step the game had started before the save may still be finished
        var afterTheLoad = PlayerPlace();
        RunFrames(50);
        var aSecondLater = PlayerPlace();

        // Assert
        Assert.Equal(afterTheLoad, aSecondLater);
    }

    // Holds the button, pressing it again each frame as a bot would, until the level runs or it's given up.
    private GamePhase HoldUntilPlaying(RetroPadButton button)
    {
        var reader = new GamePhaseReader();
        for (var frame = 0; frame < MaxFramesUntilPlaying; frame++)
        {
            Core.PressButton(button);
            Core.RunFrame();
            if (reader.Update(Core.SystemRam) == GamePhase.Playing)
                break;
        }

        return reader.Phase;
    }

    private GamePhase RunFramesWithNoInput(int frames)
    {
        var reader = new GamePhaseReader();
        for (var frame = 0; frame < frames; frame++)
        {
            Core.RunFrame();
            reader.Update(Core.SystemRam);
        }

        return reader.Phase;
    }

    private void RunFrames(int frames)
    {
        for (var i = 0; i < frames; i++)
            Core.RunFrame();
    }

    // The player's column and his place within it.
    private (byte Column, byte Step) PlayerPlace() =>
        (Core.SystemRam[RamMap.PlayerColumn], Core.SystemRam[RamMap.PlayerStepX]);

    // The level as built ($0A00-$0BC3), without the player and the guards.
    private byte[] LevelAsBuilt() => Core.SystemRam[0x0A00..0x0BC4].ToArray();
}
