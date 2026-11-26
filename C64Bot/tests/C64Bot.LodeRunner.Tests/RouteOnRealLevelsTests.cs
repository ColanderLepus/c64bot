using C64Bot.Libretro.Tests;

namespace C64Bot.LodeRunner.Tests;

[Collection(LodeRunnerCollection.Name)]
public class RouteOnRealLevelsTests(LodeRunnerReadyToStart lodeRunner)
{
    // On level 9, the nearest gold at the start leads into a pocket he can't get out of, which only the short search
    // from there sees.
    [CoreTheory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(9)]
    public void Find_RouteAfterRouteWithoutTheGuards_CompletesTheLevel(int number)
    {
        // Arrange
        lodeRunner.Starter.StartLevel(lodeRunner.Levels[number].WithoutGuards());
        lodeRunner.RunUntilSetUp();
        Assert.True(GameState.TryRead(lodeRunner.Core.SystemRam, out var state));

        // Act
        while (!state.Completed && Route.Find(state) is { } route)
            state = route.End;

        // Assert
        Assert.True(state.Completed);
    }
}
