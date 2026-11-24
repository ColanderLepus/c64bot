namespace C64Bot.Libretro;

/// <summary>
/// The libretro core that emulates the C64, as this program needs it: loaded from its DLL, set up, and with a game's
/// disk in.
/// </summary>
/// <remarks>
/// The core is VICE's x64sc emulator (D2 in <c>docs/PLAN.md</c>). Two of its core options are set: the pictures come
/// in the one format <see cref="LibretroCore"/> accepts, and the first RetroPad is the joystick in the port the game
/// reads. Another C64 core would need its own options here.
/// </remarks>
public static class C64Core
{
    /// <summary>
    /// Loads the core from its DLL, sets it up and loads the game's disk, ready to run its first frame.
    /// </summary>
    /// <param name="corePath">The core's DLL file.</param>
    /// <param name="diskPath">The game's disk image.</param>
    /// <param name="filesDirectory">
    /// A folder for the core's own files. They go in its <c>system</c> and <c>saves</c> folders, which are created if
    /// needed.
    /// </param>
    /// <param name="joystickPort">The C64's joystick port the game reads, 1 or 2.</param>
    /// <param name="log">Receives the core's log lines (see <see cref="LibretroCore.Log"/>), from the start.</param>
    /// <exception cref="DllNotFoundException">The core's DLL isn't there.</exception>
    /// <exception cref="InvalidOperationException">The core couldn't load the disk.</exception>
    public static LibretroCore Load(
        string corePath, string diskPath, string filesDirectory, int joystickPort, Action<string>? log = null)
    {
        var core = LibretroCore.Load(corePath);
        try
        {
            core.Log = log;
            core.Initialize(
                Directory.CreateDirectory(Path.Combine(filesDirectory, "system")).FullName,
                Directory.CreateDirectory(Path.Combine(filesDirectory, "saves")).FullName,
                new Dictionary<string, string>
                {
                    ["vice_gfx_colors"] = "24bit", // pictures in XRGB8888, the only format LibretroCore accepts
                    ["vice_joyport"] = $"{joystickPort}",
                });
            core.LoadGame(diskPath);
            return core;
        }
        catch
        {
            core.Dispose();
            throw;
        }
    }
}
