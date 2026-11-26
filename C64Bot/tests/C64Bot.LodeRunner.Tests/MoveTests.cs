using C64Bot.Libretro;
using static C64Bot.LodeRunner.Tests.TestStates;

namespace C64Bot.LodeRunner.Tests;

public class MoveTests
{
    // Steps and climbs

    [Fact]
    public void AllFrom_OnASolidFloor_StepsToTheNextTileEitherWay()
    {
        // Arrange
        var state = State("""
               P
            @@@@@@@
            """);

        // Act
        var moves = Move.AllFrom(state);

        // Assert
        Assert.Equal([MoveKind.Left, MoveKind.Right], moves.Select(move => move.Kind));
        var (left, right) = (moves[0], moves[1]);
        Assert.Equal(Enumerable.Repeat(Joystick.Left, 5), left.Passes);
        Assert.Equal(Enumerable.Repeat(Joystick.Right, 5), right.Passes);
        Assert.Equal((2, 0, 0, 0), Place(left.End));
        Assert.Equal((4, 0, 0, 0), Place(right.End));
    }

    [Fact]
    public void AllFrom_BesideAWall_DoesntStepThatWay()
    {
        // Arrange
        var state = State("""
            @P
            @@@
            """);

        // Act
        var moves = Move.AllFrom(state);

        // Assert
        Assert.Equal([MoveKind.Right], moves.Select(move => move.Kind));
    }

    [Fact]
    public void AllFrom_OnALadder_ClimbsOneTileUpOrDown()
    {
        // Arrange
        var state = State("""
             H
             P
             H
            @@@
            """, on: Tile.Ladder);

        // Act
        var moves = Move.AllFrom(state);

        // Assert
        var (up, down) = (Single(moves, MoveKind.Up), Single(moves, MoveKind.Down));
        Assert.Equal(Enumerable.Repeat(Joystick.Up, 5), up.Passes);
        Assert.Equal(Enumerable.Repeat(Joystick.Down, 5), down.Passes);
        Assert.Equal((1, 0, 0, 0), Place(up.End));
        Assert.Equal((1, 2, 0, 0), Place(down.End));
    }

    [Fact]
    public void AllFrom_AtTheTopOfALadder_ClimbsOntoTheTileAboveAndBack()
    {
        // Arrange
        var state = State("""

             P
            @H@
            """, on: Tile.Ladder);

        // Act
        var up = Single(Move.AllFrom(state), MoveKind.Up);
        var down = Single(Move.AllFrom(up.End), MoveKind.Down);

        // Assert
        Assert.Equal((1, 0, 0, 0), Place(up.End));
        Assert.Equal((1, 1, 0, 0), Place(down.End));
        Assert.Equal((5, 5), (up.Passes.Count, down.Passes.Count));
    }

    [Fact]
    public void AllFrom_OnARope_MovesAlongItOrLetsGo()
    {
        // Arrange
        var state = State("""
             P-


            @@@@
            """, on: Tile.Rope);

        // Act
        var moves = Move.AllFrom(state);

        // Assert
        var (right, down) = (Single(moves, MoveKind.Right), Single(moves, MoveKind.Down));
        Assert.Equal(5, right.Passes.Count);
        Assert.Equal((2, 0, 0, 0), Place(right.End));
        Assert.Equal(Enumerable.Repeat(Joystick.Down, 10), down.Passes);
        Assert.Equal((1, 2, 0, 0), Place(down.End));
    }

    [Fact]
    public void AllFrom_BetweenTwoTiles_GoesToTheMiddleOfEither()
    {
        // Arrange: right of the middle of his tile
        var state = State("""
             P
            @@@
            """, offsetX: 1);

        // Act
        var moves = Move.AllFrom(state);

        // Assert
        var (left, right) = (Single(moves, MoveKind.Left), Single(moves, MoveKind.Right));
        Assert.Equal((1, 0, 0, 0), Place(left.End));
        Assert.Equal((2, 0, 0, 0), Place(right.End));
        Assert.Equal((1, 4), (left.Passes.Count, right.Passes.Count));
    }

    // Falls

    [Fact]
    public void AllFrom_OffAnEdge_FallsUntilHeLands()
    {
        // Arrange
        var state = State("""
            P
            @
            @
            @@@
            """);

        // Act
        var fall = Single(Move.AllFrom(state), MoveKind.Right);
        var next = Single(Move.AllFrom(fall.End), MoveKind.Right);

        // Assert: he lands in the last pass, and the next move starts in the pass after it
        Assert.Equal(13, fall.Passes.Count);
        Assert.Equal((1, 2, 0, 0), Place(fall.End));
        Assert.True(fall.End.Player.Falling);
        Assert.Equal(5, next.Passes.Count);
        Assert.Equal((2, 2, 0, 0), Place(next.End));
    }

    [Fact]
    public void AllFrom_FallingOntoARope_IsCaughtByIt()
    {
        // Arrange
        var state = State("""
            P
            @
            @-
            @
            @
            @@@
            """);

        // Act
        var fall = Single(Move.AllFrom(state), MoveKind.Right);

        // Assert
        Assert.Equal(13, fall.Passes.Count);
        Assert.Equal((1, 2, 0, 0), Place(fall.End));
    }

    [Fact]
    public void AllFrom_IntoAHole_EndsAtItsBottom()
    {
        // Arrange
        var state = State("""
            P
            #o#
            ###
            """);

        // Act
        var fall = Single(Move.AllFrom(state), MoveKind.Right);

        // Assert
        Assert.Equal(8, fall.Passes.Count);
        Assert.Equal((1, 1, 0, 0), Place(fall.End));
        Assert.Equal([new Hole(new TilePosition(1, 1), 92)], fall.End.Holes);
    }

    [Fact]
    public void AllFrom_HoleClosesUnderHimInAFall_EndsWhereTheBrickStopsHim()
    {
        // Arrange: he'll be just below the middle of the tile above the hole when it closes
        var state = State("""
            P
            @ @
            @o@
            @@@
            """, holePasses: 9);

        // Act
        var fall = Single(Move.AllFrom(state), MoveKind.Right);
        var after = Move.AllFrom(fall.End);

        // Assert: walled in, with no way out
        Assert.Equal(10, fall.Passes.Count);
        Assert.Equal((1, 1, 0, 1), Place(fall.End));
        Assert.Empty(fall.End.Holes);
        Assert.Empty(after);
    }

    // Digging

    [Fact]
    public void AllFrom_OnBricks_DigsEitherWayAndTurnsFirstToDigBehindHim()
    {
        // Arrange: his last move was to the right
        var state = State("""
             P
            ####
            """, digSide: Direction.Right);

        // Act
        var moves = Move.AllFrom(state);

        // Assert
        Assert.Equal(
            [MoveKind.Left, MoveKind.Right, MoveKind.DigLeft, MoveKind.DigRight], moves.Select(move => move.Kind));
        var (left, right) = (moves[2], moves[3]);
        Assert.Equal([Joystick.Left, .. Enumerable.Repeat(Joystick.Fire, 13)], left.Passes);
        Assert.Equal(Enumerable.Repeat(Joystick.Fire, 13), right.Passes);
        Assert.Equal((1, 0, 0, 0), Place(left.End));
        Assert.Equal((1, 0, 0, 0), Place(right.End));
        Assert.Equal([new Hole(new TilePosition(0, 1), 179)], left.End.Holes);
        Assert.Equal([new Hole(new TilePosition(2, 1), 179)], right.End.Holes);
    }

    [Theory]
    [InlineData(Tile.Rope)]
    [InlineData(Tile.Ladder)]
    public void AllFrom_HangingOnARopeOrOnALadder_Digs(Tile on)
    {
        // Arrange
        var state = State("""
             P
              #
            """, on: on);

        // Act
        var dig = Single(Move.AllFrom(state), MoveKind.DigRight);

        // Assert
        Assert.Equal([new Hole(new TilePosition(2, 1), 179)], dig.End.Holes);
    }

    [Theory]
    [InlineData(" P#\n###")] // a brick beside him
    [InlineData(" P@\n###")] // a solid block beside him
    [InlineData(" PH\n###")] // a ladder beside him
    [InlineData(" P-\n###")] // a rope beside him
    [InlineData(" P$\n###")] // gold beside him
    [InlineData(" P \n##@")] // a solid block to dig
    [InlineData(" P \n##T")] // a trap brick to dig
    [InlineData(" P \n## ")] // nothing to dig
    [InlineData(" P \n##o")] // a hole there already
    public void AllFrom_WhereThereIsNoBrickToDig_DoesntDig(string text)
    {
        // Arrange
        var state = State(text, digSide: Direction.Right);

        // Act
        var moves = Move.AllFrom(state);

        // Assert
        Assert.DoesNotContain(moves, move => move.Kind == MoveKind.DigRight);
    }

    [Fact]
    public void AllFrom_AtTheSideOfTheLevel_DoesntDigPastIt()
    {
        // Arrange
        var state = State("""
            P
            ##
            """, digSide: Direction.Right);

        // Act
        var moves = Move.AllFrom(state);

        // Assert
        Assert.DoesNotContain(moves, move => move.Kind == MoveKind.DigLeft);
    }

    // Holes

    [Fact]
    public void AllFrom_WithAHoleOpen_WaitsUntilItCloses()
    {
        // Arrange
        var state = State("""
             P
            @@@o
            """, holePasses: 40);

        // Act
        var wait = Single(Move.AllFrom(state), MoveKind.WaitForHole);

        // Assert
        Assert.Equal(Enumerable.Repeat(Joystick.None, 40), wait.Passes);
        Assert.Equal((1, 0, 0, 0), Place(wait.End));
        Assert.Empty(wait.End.Holes);
    }

    [Fact]
    public void AllFrom_TrappedInAHoleThatWillClose_HasNoMoves()
    {
        // Arrange
        var state = State("""
            #P#
            ###
            """, on: Tile.Brick, standsInHole: true, holePasses: 10);

        // Act
        var moves = Move.AllFrom(state);

        // Assert
        Assert.Empty(moves);
    }

    // Gold and the end of the level

    [Fact]
    public void AllFrom_OntoGold_TakesIt()
    {
        // Arrange
        var state = State("""
            P$
            @@@
            """, goldLeft: 9);

        // Act
        var step = Single(Move.AllFrom(state), MoveKind.Right);

        // Assert
        Assert.Equal((8, 250), (step.End.GoldLeft, step.End.Score));
        Assert.Equal(Tile.Empty, step.End.Level[1, 0]);
    }

    [Fact]
    public void AllFrom_UpToTheTopRowWithNoGoldLeft_EndsWithTheLevelCompleted()
    {
        // Arrange
        var state = State("""
            H
            P
            @
            """, on: Tile.Ladder, goldLeft: 0);

        // Act
        var climb = Single(Move.AllFrom(state), MoveKind.Up);

        // Assert
        Assert.Equal(5, climb.Passes.Count);
        Assert.True(climb.End.Completed);
    }

    [Fact]
    public void AllFrom_WithNothingHoldingHim_IsAFallWhateverTheJoystick()
    {
        // Arrange
        var state = State("""
            P

            @
            """);

        // Act
        var moves = Move.AllFrom(state);

        // Assert
        var fall = Assert.Single(moves);
        Assert.Equal(MoveKind.Fall, fall.Kind);
        Assert.Equal(Enumerable.Repeat(Joystick.None, 5), fall.Passes);
        Assert.Equal((0, 1, 0, 0), Place(fall.End));
    }

    [Fact]
    public void AllFrom_WhileDigging_Throws()
    {
        // Arrange
        var state = State("""
             P
            ####
            """, digging: Direction.Right, digPasses: 3);

        // Act
        var exception = Record.Exception(() => Move.AllFrom(state));

        // Assert
        Assert.IsType<ArgumentException>(exception);
    }

    [Fact]
    public void AllFrom_AfterHeDied_Throws()
    {
        // Arrange
        var state = State("""
            #P#
            ###
            """, on: Tile.Brick, standsInHole: true, holePasses: 1).Next(Joystick.None);

        // Act
        var exception = Record.Exception(() => Move.AllFrom(state));

        // Assert
        Assert.False(state.Alive);
        Assert.IsType<ArgumentException>(exception);
    }

    private static Move Single(IReadOnlyList<Move> moves, MoveKind kind) => moves.Single(move => move.Kind == kind);

    private static (int Column, int Row, int OffsetX, int OffsetY) Place(GameState state) =>
        (state.Player.Position.Column, state.Player.Position.Row, state.Player.OffsetX, state.Player.OffsetY);
}
