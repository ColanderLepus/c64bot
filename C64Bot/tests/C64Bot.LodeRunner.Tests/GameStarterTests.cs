using C64Bot.Libretro;
using C64Bot.Libretro.Tests;

namespace C64Bot.LodeRunner.Tests;

public class GameStarterTests
{
    // Setting up a level takes about 650 frames after StartLevel; this leaves room.
    private const int FramesToSetUpALevel = 900;

    [CoreFact]
    public void StartLevel_Level5AfterLevel1_SetsUpADifferentLevelNumberedFive()
    {
        // Arrange
        using var core = LoadLodeRunner();
        var starter = GameStarter.Start(core, unlimitedLives: false);
        starter.StartLevel(1);
        RunFrames(core, FramesToSetUpALevel);
        var level1 = LevelAsBuilt(core);

        // Act
        starter.StartLevel(5);
        RunFrames(core, FramesToSetUpALevel);

        // Assert
        Assert.Equal(5, core.SystemRam[RamMap.LevelNumber]);
        Assert.Equal(4, core.SystemRam[RamMap.LevelNumberOnDisk]);
        Assert.NotEqual(level1, LevelAsBuilt(core));
    }

    private static LibretroCore LoadLodeRunner()
    {
        var directory = Path.Combine(Path.GetTempPath(), "C64Bot.LodeRunner.Tests");
        var core = LibretroCore.Load(CoreFiles.CorePath);
        core.Initialize(
            Directory.CreateDirectory(Path.Combine(directory, "system")).FullName,
            Directory.CreateDirectory(Path.Combine(directory, "saves")).FullName,
            new Dictionary<string, string> { ["vice_gfx_colors"] = "24bit", ["vice_joyport"] = "2" });
        core.LoadGame(CoreFiles.DiskPath);
        return core;
    }

    private static void RunFrames(LibretroCore core, int frames)
    {
        for (var i = 0; i < frames; i++)
            core.RunFrame();
    }

    // The level as built ($0A00-$0BC3), without the player and the guards.
    private static byte[] LevelAsBuilt(LibretroCore core) => core.SystemRam[0x0A00..0x0BC4].ToArray();
}
