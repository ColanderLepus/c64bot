namespace C64Bot.Libretro.Tests;

// A test that needs the VICE core and the Lode Runner disk. It's skipped on a PC without them.
public sealed class CoreFactAttribute : FactAttribute
{
    public CoreFactAttribute()
    {
        if (!CoreFiles.Exist)
            Skip = $"Needs {CoreFiles.CorePath} and {CoreFiles.DiskPath}.";
    }
}
