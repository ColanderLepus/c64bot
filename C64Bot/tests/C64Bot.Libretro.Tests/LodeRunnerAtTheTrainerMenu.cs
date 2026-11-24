namespace C64Bot.Libretro.Tests;

// The VICE core with Lode Runner loaded and run flat out to its trainer menu, which takes 2,532 frames. Shared
// by the tests in CoreCollection, so the loading happens only once.
public sealed class LodeRunnerAtTheTrainerMenu : IDisposable
{
    private const int MaxFramesToTheMenu = 3000;

    private readonly LibretroCore? _core;

    public LodeRunnerAtTheTrainerMenu()
    {
        if (!CoreFiles.Exist)
            return;

        _core = C64Core.Load(
            CoreFiles.CorePath, CoreFiles.DiskPath, Path.Combine(Path.GetTempPath(), "C64Bot.Libretro.Tests"),
            joystickPort: 2);

        for (var frame = 0; !ScreenText().Contains("UNLIMITED LIVES"); frame++)
        {
            if (frame == MaxFramesToTheMenu)
                throw new InvalidOperationException($"The trainer menu didn't appear within {MaxFramesToTheMenu} frames.");

            _core.RunFrame();
        }
    }

    public LibretroCore Core => _core ?? throw new InvalidOperationException("The core or the disk isn't there.");

    public string ScreenText() => TextScreen.Read(Core.SystemRam);

    public void Dispose() => _core?.Dispose();
}
