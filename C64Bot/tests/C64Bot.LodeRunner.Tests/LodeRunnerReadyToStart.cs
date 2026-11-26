using C64Bot.Libretro;
using C64Bot.Libretro.Tests;

namespace C64Bot.LodeRunner.Tests;

// The VICE core with Lode Runner loaded and run to the start of a new game (GameStarter.Start), which takes about
// 5 seconds. Shared by the tests in LodeRunnerCollection, so that happens only once. Each test starts its own game
// with StartLevel, which lets go of all input, so the tests don't depend on each other.
public sealed class LodeRunnerReadyToStart : IDisposable
{
    private readonly LibretroCore? _core;
    private readonly GameStarter? _starter;
    private readonly LevelDisk? _levels;

    public LodeRunnerReadyToStart()
    {
        if (!CoreFiles.Exist)
            return;

        _core = C64Core.Load(
            CoreFiles.CorePath, CoreFiles.DiskPath, Path.Combine(Path.GetTempPath(), "C64Bot.LodeRunner.Tests"),
            joystickPort: 2);
        _starter = GameStarter.Start(_core, unlimitedLives: true);
        _levels = new LevelDisk(DiskImage.Load(CoreFiles.DiskPath));
    }

    public LibretroCore Core => _core ?? throw new InvalidOperationException("The core or the disk isn't there.");

    public GameStarter Starter => _starter ?? throw new InvalidOperationException("The core or the disk isn't there.");

    public LevelDisk Levels => _levels ?? throw new InvalidOperationException("The core or the disk isn't there.");

    // Runs until the level is set up and waits for the first move, about 490 frames after StartLevel, or 640 if the
    // game reads the level from the disk.
    public void RunUntilSetUp()
    {
        for (var frame = 0; frame < 1200; frame++)
        {
            Core.RunFrame();
            if (GameState.TryRead(Core.SystemRam, out var state) && state is { Alive: true, Completed: false })
                return;
        }

        Assert.Fail("The level wasn't set up.");
    }

    public void Dispose() => _core?.Dispose();
}
