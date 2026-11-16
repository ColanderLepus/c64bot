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

    public LodeRunnerReadyToStart()
    {
        if (!CoreFiles.Exist)
            return;

        var directory = Path.Combine(Path.GetTempPath(), "C64Bot.LodeRunner.Tests");
        _core = LibretroCore.Load(CoreFiles.CorePath);
        _core.Initialize(
            Directory.CreateDirectory(Path.Combine(directory, "system")).FullName,
            Directory.CreateDirectory(Path.Combine(directory, "saves")).FullName,
            new Dictionary<string, string> { ["vice_gfx_colors"] = "24bit", ["vice_joyport"] = "2" });
        _core.LoadGame(CoreFiles.DiskPath);
        _starter = GameStarter.Start(_core, unlimitedLives: true);
    }

    public LibretroCore Core => _core ?? throw new InvalidOperationException("The core or the disk isn't there.");

    public GameStarter Starter => _starter ?? throw new InvalidOperationException("The core or the disk isn't there.");

    public void Dispose() => _core?.Dispose();
}
