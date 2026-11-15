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

        var directory = Path.Combine(Path.GetTempPath(), "C64Bot.Libretro.Tests");
        _core = LibretroCore.Load(CoreFiles.CorePath);
        _core.Initialize(
            Directory.CreateDirectory(Path.Combine(directory, "system")).FullName,
            Directory.CreateDirectory(Path.Combine(directory, "saves")).FullName,
            new Dictionary<string, string> { ["vice_gfx_colors"] = "24bit", ["vice_joyport"] = "2" });
        _core.LoadGame(CoreFiles.DiskPath);

        for (var frame = 0; !ScreenText().Contains("UNLIMITED LIVES"); frame++)
        {
            if (frame == MaxFramesToTheMenu)
                throw new InvalidOperationException($"The trainer menu didn't appear within {MaxFramesToTheMenu} frames.");

            _core.RunFrame();
        }
    }

    public LibretroCore Core => _core ?? throw new InvalidOperationException("The core or the disk isn't there.");

    // The C64's 40×25 text screen at $0400 as one string, row after row. Screen codes 1-26 are A-Z and 32-63 match
    // ASCII; bit 7 only means the character is shown reversed.
    public string ScreenText()
    {
        var screen = Core.SystemRam[0x0400..0x07E8];
        var text = new char[screen.Length];
        for (var i = 0; i < screen.Length; i++)
        {
            var code = screen[i] & 0x7F;
            text[i] = code switch { < 32 => (char)('@' + code), < 64 => (char)code, _ => '#' };
        }

        return new string(text);
    }

    public void Dispose() => _core?.Dispose();
}
