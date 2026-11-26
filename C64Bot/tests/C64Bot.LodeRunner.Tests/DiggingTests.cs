using C64Bot.Libretro;
using C64Bot.Libretro.Tests;

namespace C64Bot.LodeRunner.Tests;

// Digs in the running game, as GameState reads them.
[Collection(LodeRunnerCollection.Name)]
public class DiggingTests(LodeRunnerReadyToStart lodeRunner)
{
    private const int MaxFrames = 200;

    // Goes up by one in every pass, from 0 to 27 and then again from 0 (the column guards respawn from).
    private const int PassCounter = 0x131E;
    private const int PassCounterValues = 28;

    private LibretroCore Core => lodeRunner.Core;

    [CoreTheory]
    [InlineData(RetroPadButton.Left, Direction.Left, 3)]
    [InlineData(RetroPadButton.Right, Direction.Right, 5)]
    public void TryRead_DigOnTheFloor_FollowsItPassByPassUntilTheHoleOpens(
        RetroPadButton move, Direction side, int holeColumn)
    {
        // Arrange: he takes a step to the side
        StartLevel("""

                P     $
            ###########
            """);
        Hold(move, until: state => state.Player.OffsetX != 0);

        // Act
        var readings = Hold(RetroPadButton.B, until: state => state.Holes.Count > 0);

        // Assert
        AssertDig(readings, side, new TilePosition(holeColumn, 2));
    }

    [CoreTheory]
    [InlineData(RetroPadButton.Left, Direction.Left, "  HP", 1)]
    [InlineData(RetroPadButton.Right, Direction.Right, " PH", 3)]
    public void TryRead_DigOnALadder_FollowsItPassByPassUntilTheHoleOpens(
        RetroPadButton move, Direction side, string playersRow, int holeColumn)
    {
        // Arrange: he walks to the ladder and climbs up through the bricks
        StartLevel($"""

              H
            ##H##
            {playersRow}
            #####   $
            """);
        Hold(move, until: state => state.Player.Position.Column == 2 && state.Player.OffsetX == 0);
        var climb = Hold(
            RetroPadButton.Up, until: state => state.Player.Position.Row == 1 && state.Player.OffsetY == 0);
        var onLadder = climb[^1].State.Player;

        // Act
        var readings = Hold(RetroPadButton.B, until: state => state.Holes.Count > 0);

        // Assert
        Assert.Null(onLadder.Facing);
        Assert.Equal(side, onLadder.DigSide);
        AssertDig(readings, side, new TilePosition(holeColumn, 2));
    }

    [CoreTheory]
    [InlineData(RetroPadButton.Left, Direction.Left, "     ---P", "#####   #", 5)]
    [InlineData(RetroPadButton.Right, Direction.Right, "P---", "#   #####", 3)]
    public void TryRead_DigHangingAtTheEndOfARope_FollowsItPassByPassUntilTheHoleOpens(
        RetroPadButton move, Direction side, string playersRow, string bricks, int ropesEnd)
    {
        // Arrange: he moves along the rope to its end, with nothing below him
        StartLevel($"""

            {playersRow}
            {bricks}

            #########   $
            """);
        var alongRope = Hold(
            move, until: state => state.Player.Position.Column == ropesEnd && state.Player.OffsetX == 0);
        var onRope = alongRope[^1].State;

        // Act
        var readings = Hold(RetroPadButton.B, until: state => state.Holes.Count > 0);

        // Assert
        Assert.Equal(Tile.Rope, onRope.Level[onRope.Player.Position]);
        Assert.Equal(Tile.Empty, onRope.Level[ropesEnd, 2]);
        Assert.All(readings, reading => Assert.False(reading.State.Player.Falling));
        AssertDig(readings, side, new TilePosition(4, 2));
    }

    // From the dig's first pass on, every state is as many passes into the dig as the game has run, until the hole is
    // there. A pass can be missing: when one takes long, the next starts before a frame ends.
    private static void AssertDig(List<(GameState State, int Pass)> readings, Direction side, TilePosition hole)
    {
        var dig = readings.SkipWhile(reading => reading.State.Player.Digging is null).ToList();
        var firstPass = dig[0].Pass;
        var passes = dig.Select(reading => 1 + (reading.Pass - firstPass + PassCounterValues) % PassCounterValues).ToList();

        Assert.Equal(passes.SkipLast(1), dig.SkipLast(1).Select(reading => reading.State.Player.DigPasses));
        Assert.All(dig.SkipLast(1), reading => Assert.Equal(side, reading.State.Player.Digging));
        Assert.All(dig, reading => Assert.Equal(side, reading.State.Player.DigSide));
        Assert.All(dig.SkipLast(1), reading => Assert.InRange(reading.State.Player.DigPasses, 1, 12));

        var opened = dig[^1].State;
        Assert.InRange(passes[^1], 13, 14);
        Assert.Null(opened.Player.Digging);
        Assert.Equal(0, opened.Player.DigPasses);
        Assert.Equal([new Hole(hole, Hole.OpenPasses + 12 - passes[^1])], opened.Holes);
    }

    private void StartLevel(string text)
    {
        lodeRunner.Starter.StartLevel(Level.Parse(1, text));
        lodeRunner.RunUntilSetUp();
    }

    // Holds the button until a state fits, and gives every state read on the way, with the pass counter.
    private List<(GameState State, int Pass)> Hold(RetroPadButton button, Func<GameState, bool> until)
    {
        var readings = new List<(GameState State, int Pass)>();
        Core.PressButton(button);
        try
        {
            for (var frame = 0; frame < MaxFrames; frame++)
            {
                Core.RunFrame();
                if (!GameState.TryRead(Core.SystemRam, out var state))
                    continue;

                readings.Add((state, Core.SystemRam[PassCounter]));
                if (until(state))
                    return readings;
            }
        }
        finally
        {
            Core.ReleaseButton(button);
        }

        Assert.Fail($"It didn't happen within {MaxFrames} frames, holding {button}.");
        return readings;
    }
}
