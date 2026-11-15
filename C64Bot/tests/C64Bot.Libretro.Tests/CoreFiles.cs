namespace C64Bot.Libretro.Tests;

// Where the tests find the VICE core and the Lode Runner disk (see Environment in docs/PLAN.md). The tests that
// need them are skipped on a PC without them.
internal static class CoreFiles
{
    public const string CorePath = @"C:\Tools\C64\libretro\vice_x64sc_libretro.dll";
    public const string DiskPath = @"C:\Emulation\ROMs\C64\Lode Runner.d64";

    public static bool Exist => File.Exists(CorePath) && File.Exists(DiskPath);
}
