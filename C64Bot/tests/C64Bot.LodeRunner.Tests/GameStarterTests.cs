using System.Security.Cryptography;
using C64Bot.Libretro;
using C64Bot.Libretro.Tests;

namespace C64Bot.LodeRunner.Tests;

[Collection(LodeRunnerCollection.Name)]
public class GameStarterTests(LodeRunnerReadyToStart lodeRunner)
{
    private const int MaxFrames = 2500;

    private LibretroCore Core => lodeRunner.Core;

    [CoreFact]
    public void StartLevel_Level5AfterLevel1_SetsUpADifferentLevelNumberedFive()
    {
        // Arrange
        lodeRunner.Starter.StartLevel(lodeRunner.Levels[1]);
        lodeRunner.RunUntilSetUp();
        var level1 = LevelAsBuilt();

        // Act
        lodeRunner.Starter.StartLevel(lodeRunner.Levels[5]);
        lodeRunner.RunUntilSetUp();

        // Assert
        Assert.Equal(5, Core.SystemRam[RamMap.LevelNumber]);
        Assert.Equal(4, Core.SystemRam[RamMap.LevelNumberOnDisk]);
        Assert.NotEqual(level1, LevelAsBuilt());
    }

    [CoreFact]
    public void StartLevel_WithoutAMove_KeepsWaitingForTheFirstMove()
    {
        // Arrange
        lodeRunner.Starter.StartLevel(lodeRunner.Levels[1]);

        // Act
        var phase = RunFramesWithNoInput(MaxFrames);

        // Assert
        Assert.Equal(GamePhase.LevelStarting, phase);
    }

    [CoreFact]
    public void StartLevel_SameMoveHeldBeforeAndAfter_StartsTheLevel()
    {
        // Arrange: level 1 running, started by holding left, and left still held
        lodeRunner.Starter.StartLevel(lodeRunner.Levels[1]);
        var firstTry = HoldUntilPlaying(RetroPadButton.Left);

        // Act: a new try at the same level, holding left again
        lodeRunner.Starter.StartLevel(lodeRunner.Levels[1]);
        var secondTry = HoldUntilPlaying(RetroPadButton.Left);

        // Assert
        Assert.Equal(GamePhase.Playing, firstTry);
        Assert.Equal(GamePhase.Playing, secondTry);
    }

    [CoreFact]
    public void LoadState_StateSavedWhileWalking_ComesBackWithThePlayerStandingStill()
    {
        // Arrange: level 1 running, the player walking left when the state is saved; then let go, so the core
        // last saw no buttons before the load
        lodeRunner.Starter.StartLevel(lodeRunner.Levels[1]);
        HoldUntilPlaying(RetroPadButton.Left);
        Core.RunFrames(10);
        var walking = Core.SaveState();
        Core.ReleaseButton(RetroPadButton.Left);
        Core.RunFrames(5);

        // Act
        Core.LoadState(walking);
        Core.RunFrames(10); // a step the game had started before the save may still be finished
        var afterTheLoad = PlayerPlace();
        Core.RunFrames(50);
        var aSecondLater = PlayerPlace();

        // Assert
        Assert.Equal(afterTheLoad, aSecondLater);
    }

    [CoreTheory]
    [InlineData(1)]
    [InlineData(150)]
    public void StartLevel_LevelFromTheDisk_IsSetUpTheSameAsWhenTheGameReadsIt(int number)
    {
        // Arrange: a changed level in the buffer, and the game made to read the level from the disk instead
        var level = lodeRunner.Levels[number];
        lodeRunner.Starter.StartLevel(level.WithoutGuards());
        Core.WriteSystemRam(RamMap.LevelInBuffer, [0xFF]);
        lodeRunner.RunUntilSetUp();
        var fromDisk = Core.SystemRam.ToArray();

        // Act
        lodeRunner.Starter.StartLevel(level);
        lodeRunner.RunUntilSetUp();
        var fromLevel = Core.SystemRam.ToArray();

        // Assert
        AssertSame(0x0800..0x09C4); // the level as played
        AssertSame(0x0A00..0x0BC4); // the level as built
        AssertSame(0x1000..0x10E0); // the level buffer
        AssertSame(0x1200..0x1300); // hidden ladders, guards and holes

        void AssertSame(Range range) => Assert.Equal(fromDisk[range], fromLevel[range]);
    }

    [CoreFact]
    public void StartLevel_WithoutGuards_HasNoneAlsoAfterADeath()
    {
        // Arrange
        var level = lodeRunner.Levels[1].WithoutGuards();

        // Act: play until the player dies in his own hole, then start the same level again
        lodeRunner.Starter.StartLevel(level);
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
        Assert.DoesNotContain((byte)Tile.Guard, Grid(ramAfter, RamMap.LevelAsPlayed));
    }

    [CoreFact]
    public void StartLevel_SameLevelAndInputTwice_RepeatsExactly()
    {
        // Arrange
        var level = lodeRunner.Levels[1];

        // Act
        var first = Play(out var framesPlaying);
        var second = Play(out _);

        // Assert
        Assert.True(framesPlaying > 300, $"The level was only played for {framesPlaying} frames.");
        Assert.Equal(first, second);

        // The RAM after every frame, as a hash: left until the level runs, then a fixed mix of moves and digging.
        List<string> Play(out int framesPlaying)
        {
            RetroPadButton[] buttons =
                [RetroPadButton.Right, RetroPadButton.B, RetroPadButton.Up, RetroPadButton.Left, RetroPadButton.Down];
            var hashes = new List<string>();
            var reader = new GamePhaseReader();
            framesPlaying = 0;
            lodeRunner.Starter.StartLevel(level);
            for (var frame = 0; frame < 1500; frame++)
            {
                var button = frame < 600 ? RetroPadButton.Left : buttons[frame / 23 % buttons.Length];
                Core.PressButton(button);
                Core.RunFrame();
                Core.ReleaseButton(button);
                if (reader.Update(Core.SystemRam) == GamePhase.Playing)
                    framesPlaying++;
                hashes.Add(Convert.ToHexString(SHA256.HashData(Core.SystemRam)));
            }

            return hashes;
        }
    }

    [CoreFact]
    public void StartLevel_MadeUpLevel_IsSetUpAsWritten()
    {
        // Arrange
        var level = Level.Parse(42, """
                h
                h
            $   H---   G
            ####H#  ####
              T H
            P   H  $
            @@@@@@@@@@@@
            """);

        // Act
        lodeRunner.Starter.StartLevel(level);
        lodeRunner.RunUntilSetUp();
        var ram = Core.SystemRam.ToArray();

        // Assert
        Assert.True(GameState.TryRead(ram, out var state));
        var builtTiles = level.Replace(Tile.HiddenLadder, Tile.Empty).WithoutGuards().Replace(Tile.Player, Tile.Empty);
        Assert.Equal(builtTiles.ToString(), state.Level.ToString());
        Assert.Equal(level.HiddenLadders, state.Level.HiddenLadders.OrderBy(ladder => ladder.Row));
        Assert.Equal(42, state.Level.Number);
        Assert.Equal(new TilePosition(0, 5), state.Player.Position);
        Assert.Equal([new TilePosition(11, 2)], state.Guards.Select(guard => guard.Position));
        Assert.Equal(2, state.GoldLeft);
    }

    // The game keeps only 45 of the level's 48 hidden ladders, so the other three never appear.
    [CoreFact]
    public void StartLevel_Level89_AllGoldTaken_ShowsTheLaddersTheLevelLists()
    {
        // Arrange: no guards, and all gold gone except one piece beside the player
        var stored = lodeRunner.Levels[89];
        var level = stored.WithoutGuards().Replace(Tile.Gold, Tile.Empty).With(5, 2, Tile.Gold);
        lodeRunner.Starter.StartLevel(level);
        lodeRunner.RunUntilSetUp();
        var listed = State()!.Level.HiddenLadders;

        // Act: the ladders appear one after another, over several frames
        var frames = new List<Level>();
        RunUntil(new GamePhaseReader(), RetroPadButton.Right, () =>
        {
            frames.Add(Level.Read(Core.SystemRam));
            return Core.SystemRam[RamMap.GoldLeft] == 0xFF;
        });
        Core.RunFrames(30);
        var after = State()!.Level;

        // Assert
        Assert.Equal(45, listed.Count);
        Assert.Contains(frames, frame => frame.HiddenLadders.Count is > 0 and < 45);
        Assert.All(frames, frame => Assert.Equal(
            listed.Where(position => frame[position] != Tile.Ladder),
            frame.HiddenLadders));
        Assert.Empty(after.HiddenLadders);
        Assert.All(listed, position => Assert.Equal(Tile.Ladder, after[position]));
        Assert.Equal(
            [new TilePosition(1, 0), new TilePosition(2, 1), new TilePosition(3, 1)],
            stored.HiddenLadders.Except(listed));
        Assert.All(stored.HiddenLadders.Except(listed), position => Assert.Equal(Tile.Empty, after[position]));
    }

    [CoreFact]
    public void StartLevel_MadeUpLevelCompleted_LoadsTheNextOneFromTheDisk()
    {
        // Arrange
        var level2 = lodeRunner.Levels[2];
        var level = Level.Parse(1, """
               H
               H
            P$ H
            ######
            """);

        // Act: take the gold and climb out
        lodeRunner.Starter.StartLevel(level);
        var reader = new GamePhaseReader();
        RunUntil(reader, RetroPadButton.Right, () => State()?.Player.Position.Column == 3);
        RunUntil(reader, RetroPadButton.Up, () => Core.SystemRam[RamMap.LevelNumber] == 2);
        lodeRunner.RunUntilSetUp();
        var ram = Core.SystemRam.ToArray();

        // Assert
        Assert.Equal(level2.ToBytes(), ram[0x1000..0x10E0]);
        Assert.True(GameState.TryRead(ram, out var state));
        Assert.NotEmpty(state.Guards);
    }

    // Holds the button, pressing it again each frame as a bot would, until the level runs or it's given up.
    private GamePhase HoldUntilPlaying(RetroPadButton button)
    {
        var reader = new GamePhaseReader();
        for (var frame = 0; frame < MaxFrames; frame++)
        {
            Core.PressButton(button);
            Core.RunFrame();
            if (reader.Update(Core.SystemRam) == GamePhase.Playing)
                break;
        }

        return reader.Phase;
    }

    private GamePhase RunFramesWithNoInput(int frames)
    {
        var reader = new GamePhaseReader();
        for (var frame = 0; frame < frames; frame++)
        {
            Core.RunFrame();
            reader.Update(Core.SystemRam);
        }

        return reader.Phase;
    }

    // The player's column and his place within it.
    private (byte Column, byte Step) PlayerPlace() =>
        (Core.SystemRam[RamMap.PlayerColumn], Core.SystemRam[RamMap.PlayerStepX]);

    // The level as built ($0A00-$0BC3), without the player and the guards.
    private byte[] LevelAsBuilt() => Core.SystemRam[0x0A00..0x0BC4].ToArray();

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

    // One of the two grids, without the gap after row 8.
    private static byte[] Grid(byte[] ram, int grid) =>
        Enumerable.Range(0, 16 * 28).Select(i => ram[RamMap.TileAddress(grid, i % 28, i / 28)]).ToArray();
}
