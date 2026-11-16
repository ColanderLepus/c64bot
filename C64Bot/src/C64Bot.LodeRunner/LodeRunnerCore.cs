using C64Bot.Libretro;

namespace C64Bot.LodeRunner;

/// <summary>
/// The VICE libretro core as Lode Runner needs it: loaded from its DLL, set up, and with the game's disk in.
/// </summary>
/// <remarks>
/// VICE is a C64 emulator; the core is its x64sc build as a libretro core (D2 in <c>docs/PLAN.md</c>). Two of its
/// core options are set: the pictures come in the one format <see cref="LibretroCore"/> accepts, and the first
/// RetroPad is the joystick in port 2, the one Lode Runner reads.
/// </remarks>
public static class LodeRunnerCore
{
    /// <summary>
    /// Loads the core from its DLL, sets it up and loads Lode Runner's disk, ready to run its first frame.
    /// </summary>
    /// <param name="corePath">The core's DLL file.</param>
    /// <param name="diskPath">The Lode Runner disk image (version A, D6).</param>
    /// <param name="filesDirectory">
    /// A folder for the core's own files. They go in its <c>system</c> and <c>saves</c> folders, which are created if
    /// needed.
    /// </param>
    /// <param name="log">Receives the core's log lines (see <see cref="LibretroCore.Log"/>), from the start.</param>
    /// <exception cref="DllNotFoundException">The core's DLL isn't there.</exception>
    /// <exception cref="InvalidOperationException">The core couldn't load the disk.</exception>
    public static LibretroCore Load(string corePath, string diskPath, string filesDirectory, Action<string>? log = null)
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
                    ["vice_joyport"] = "2",        // the first RetroPad is the joystick in port 2
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
