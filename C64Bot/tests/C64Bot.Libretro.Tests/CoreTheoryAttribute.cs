namespace C64Bot.Libretro.Tests;

// A CoreFact with data: a theory that needs the VICE core and the Lode Runner disk.
public sealed class CoreTheoryAttribute : TheoryAttribute
{
    public CoreTheoryAttribute()
    {
        if (!CoreFiles.Exist)
            Skip = $"Needs {CoreFiles.CorePath} and {CoreFiles.DiskPath}.";
    }
}
