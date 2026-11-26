using C64Bot.Libretro.Tests;

namespace C64Bot.LodeRunner.Tests;

// The race over all 150 levels, which takes minutes. Skipped unless the environment variable asks for it.
public sealed class FullRaceFactAttribute : FactAttribute
{
    private const string Variable = "C64BOT_FULL_RACE";

    public FullRaceFactAttribute()
    {
        if (Environment.GetEnvironmentVariable(Variable) != "1")
            Skip = $"Takes minutes. Runs only with {Variable}=1.";
        else if (!CoreFiles.Exist)
            Skip = $"Needs {CoreFiles.CorePath} and {CoreFiles.DiskPath}.";
    }
}
