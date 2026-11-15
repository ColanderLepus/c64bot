namespace C64Bot.Libretro.Tests;

// The tests that load the VICE core run one after the other and share one running core, because only one core
// can run in a process at a time.
[CollectionDefinition(Name)]
public sealed class CoreCollection : ICollectionFixture<LodeRunnerAtTheTrainerMenu>
{
    public const string Name = "VICE core";
}
