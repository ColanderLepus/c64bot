namespace C64Bot.LodeRunner.Tests;

// The tests that run Lode Runner in the VICE core run one after the other and share one running core, because only
// one core can run in a process at a time.
[CollectionDefinition(Name)]
public sealed class LodeRunnerCollection : ICollectionFixture<LodeRunnerReadyToStart>
{
    public const string Name = "Lode Runner in the VICE core";
}
