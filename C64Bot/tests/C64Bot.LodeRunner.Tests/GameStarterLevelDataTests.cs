using System.Security.Cryptography;
using C64Bot.Libretro;
using C64Bot.Libretro.Tests;

namespace C64Bot.LodeRunner.Tests;

// Levels of our own put into the game: GameStarter.StartLevel(LevelData).
[Collection(LodeRunnerCollection.Name)]
public class GameStarterLevelDataTests(LodeRunnerReadyToStart lodeRunner)
{
    // By now a level from the disk (about 640 frames) or from data (about 490) waits for the first move.
    private const int FramesUntilWaiting = 1200;
    private const int MaxFrames = 2500;

    private LibretroCore Core => lodeRunner.Core;

    [CoreTheory]
    [InlineData(1)]
    [InlineData(150)]
    public void StartLevel_UnchangedData_SetsUpTheSameAsFromTheDisk(int level)
    {
        // Arrange
        var data = LevelData.FromDisk(Disk(), level);
        lodeRunner.Starter.StartLevel(level);
        RunFrames(FramesUntilWaiting);
        var fromDisk = Core.SystemRam.ToArray();

        // Act
        lodeRunner.Starter.StartLevel(data);
        RunFrames(FramesUntilWaiting);
        var fromData = Core.SystemRam.ToArray();

        // Assert
        AssertSame(0x0800..0x09C4); // the level as played
        AssertSame(0x0A00..0x0BC4); // the level as built
        AssertSame(0x1000..0x10E0); // the level buffer
        AssertSame(0x1200..0x1300); // hidden ladders, guards and holes

        void AssertSame(Range range) => Assert.Equal(fromDisk[range], fromData[range]);
    }

    [CoreFact]
    public void StartLevel_WithoutGuards_HasNoneAlsoAfterADeath()
    {
        // Arrange
        var data = LevelData.FromDisk(Disk(), 1).WithoutGuards();

        // Act: play until the player dies in his own hole, then start the same level again
        lodeRunner.Starter.StartLevel(data);
        var reader = new GamePhaseReader();
        RunUntil(reader, RetroPadButton.Left, () => reader.Phase == GamePhase.Playing);
        var guardsBefore = Core.SystemRam[RamMap.GuardCount];
        RunUntil(reader, RetroPadButton.B, () => State()?.Holes.Count == 1);
        var hole = State()!.Holes[0].Position;
        RunUntil(reader, RetroPadButton.Left, () => State()?.Player.Position == hole);
        RunUntil(reader, null, () => reader.Phase == GamePhase.Dying);
        RunUntil(reader, RetroPadButton.Left, () => reader.Phase == GamePhase.Playing);
        var ramAfter = Core.SystemRam.ToArray();

        // Assert
        Assert.Equal(0, guardsBefore);
        Assert.Equal(0, ramAfter[RamMap.GuardCount]);
        Assert.DoesNotContain((byte)LevelTile.Guard, Grid(ramAfter, RamMap.LevelAsPlayed));
    }

    [CoreFact]
    public void StartLevel_SameDataAndInputTwice_RepeatsExactly()
    {
        // Arrange
        var data = LevelData.FromDisk(Disk(), 1);

        // Act
        var first = Play();
        var second = Play();

        // Assert
        Assert.Equal(first, second);

        // The RAM after every frame, as a hash: left until the level runs, then a fixed mix of moves and digging.
        List<string> Play()
        {
            RetroPadButton[] buttons =
                [RetroPadButton.Right, RetroPadButton.B, RetroPadButton.Up, RetroPadButton.Left, RetroPadButton.Down];
            var hashes = new List<string>();
            lodeRunner.Starter.StartLevel(data);
            for (var frame = 0; frame < 1500; frame++)
            {
                var button = frame < 600 ? RetroPadButton.Left : buttons[frame / 23 % buttons.Length];
                Core.PressButton(button);
                Core.RunFrame();
                Core.ReleaseButton(button);
                hashes.Add(Convert.ToHexString(SHA256.HashData(Core.SystemRam)));
            }

            return hashes;
        }
    }

    [CoreFact]
    public void StartLevel_MadeUpLevel_IsSetUpAsWritten()
    {
        // Arrange
        var data = LevelData.Parse(42, """
                h
                h
            $   H---   G
            ####H#  ####
              T H
            P   H  $
            @@@@@@@@@@@@
            """);

        // Act
        lodeRunner.Starter.StartLevel(data);
        RunFrames(FramesUntilWaiting);
        var ram = Core.SystemRam.ToArray();

        // Assert
        var expected = data.Replace(LevelTile.HiddenLadder, LevelTile.Empty).WithoutGuards()
            .Replace(LevelTile.Player, LevelTile.Empty);
        Assert.Equal(Enumerable.Range(0, 16 * 28).Select(i => (byte)expected[i % 28, i / 28]),
            Grid(ram, RamMap.LevelAsBuilt));
        Assert.True(GameState.TryRead(ram, out var state));
        Assert.Equal(42, state.LevelNumber);
        Assert.Equal(new TilePosition(0, 5), state.Player.Position);
        Assert.Equal([new TilePosition(11, 2)], state.Guards.Select(guard => guard.Position));
        Assert.Equal(2, state.GoldLeft);
        Assert.Equal(
            [new TilePosition(4, 0), new TilePosition(4, 1)],
            state.Level.HiddenLadders.OrderBy(ladder => ladder.Row));
    }

    [CoreFact]
    public void StartLevel_MadeUpLevelCompleted_LoadsTheNextOneFromTheDisk()
    {
        // Arrange
        var level2 = LevelData.FromDisk(Disk(), 2);
        var data = LevelData.Parse(1, """
               H
               H
            P$ H
            ######
            """);

        // Act: take the gold and climb out
        lodeRunner.Starter.StartLevel(data);
        var reader = new GamePhaseReader();
        RunUntil(reader, RetroPadButton.Right, () => State()?.Player.Position.Column == 3);
        RunUntil(reader, RetroPadButton.Up, () => Core.SystemRam[RamMap.LevelNumber] == 2);
        RunFrames(FramesUntilWaiting);
        var ram = Core.SystemRam.ToArray();

        // Assert
        Assert.Equal(level2.ToBytes(), ram[0x1000..0x10E0]);
        Assert.True(GameState.TryRead(ram, out var state));
        Assert.NotEmpty(state.Guards);
    }

    private static DiskImage Disk() => DiskImage.Load(CoreFiles.DiskPath);

    // Holds the button, if any, until the condition is met.
    private void RunUntil(GamePhaseReader reader, RetroPadButton? button, Func<bool> condition)
    {
        if (button is { } pressed)
            Core.PressButton(pressed);
        try
        {
            for (var frame = 0; frame < MaxFrames; frame++)
            {
                Core.RunFrame();
                reader.Update(Core.SystemRam);
                if (condition())
                    return;
            }
        }
        finally
        {
            if (button is { } held)
                Core.ReleaseButton(held);
        }

        Assert.Fail($"It didn't happen within {MaxFrames} frames, holding {button?.ToString() ?? "nothing"}.");
    }

    private GameState? State() => GameState.TryRead(Core.SystemRam, out var state) ? state : null;

    private void RunFrames(int frames)
    {
        for (var i = 0; i < frames; i++)
            Core.RunFrame();
    }

    // One of the two grids, without the gap after row 8.
    private static byte[] Grid(byte[] ram, int grid) =>
        Enumerable.Range(0, 16 * 28).Select(i => ram[RamMap.TileAddress(grid, i % 28, i / 28)]).ToArray();
}
