using C64Bot.Libretro;

namespace C64Bot.LodeRunner.Tests;

public class GameStateNextTests
{
    // Walking

    [Theory]
    [InlineData(Joystick.Right, 1)]
    [InlineData(Joystick.Left, -1)]
    public void Next_WalkingOnTheFloor_TakesFivePassesToTheNextTile(Joystick joystick, int step)
    {
        // Arrange
        var state = State("""
            P
            #######
            """, column: 3);

        // Act
        var places = Run(state, joystick, passes: 5).Select(Place);

        // Assert
        Assert.Equal(
            [
                (3, 0, step, 0), (3, 0, 2 * step, 0), (3 + step, 0, -2 * step, 0), (3 + step, 0, -step, 0),
                (3 + step, 0, 0, 0),
            ],
            places);
    }

    [Fact]
    public void Next_TurningHalfwayToTheNextTile_GoesBack()
    {
        // Arrange
        var state = State("""
            P
            #######
            """, column: 3, offsetX: 2);

        // Act
        var next = state.Next(Joystick.Left);

        // Assert
        Assert.Equal((3, 0, 1, 0), Place(next));
        Assert.Equal(Direction.Left, next.Player.Facing);
    }

    [Theory]
    [InlineData('#', Joystick.Left)]
    [InlineData('@', Joystick.Left)]
    [InlineData('T', Joystick.Left)]
    [InlineData('#', Joystick.Right)]
    [InlineData('@', Joystick.Right)]
    [InlineData('T', Joystick.Right)]
    public void Next_WallBesideHim_StopsHimInTheMiddleOfHisTile(char wall, Joystick joystick)
    {
        // Arrange
        var state = State($"""
            {wall}P{wall}
            ###
            """, offsetX: joystick == Joystick.Left ? 1 : -1);

        // Act
        var places = Run(state, joystick, passes: 2).Select(Place);

        // Assert
        Assert.Equal([(1, 0, 0, 0), (1, 0, 0, 0)], places);
    }

    [Theory]
    [InlineData(0, Joystick.Left)]
    [InlineData(27, Joystick.Right)]
    public void Next_AtTheLevelsSide_StopsHimInTheMiddleOfHisTile(int column, Joystick joystick)
    {
        // Arrange
        var state = State("P\n" + new string('#', 28), column: column);

        // Act
        var next = state.Next(joystick);

        // Assert
        Assert.Equal((column, 0, 0, 0), Place(next));
    }

    [Theory]
    [InlineData(' ')]
    [InlineData('H')]
    [InlineData('-')]
    [InlineData('$')]
    [InlineData('h')]
    [InlineData('o')]
    public void Next_AnythingButAWallBesideHim_LetsHimPass(char beside)
    {
        // Arrange
        var state = State($"""
            P{beside}
            ##
            """, offsetX: 2);

        // Act
        var next = state.Next(Joystick.Right);

        // Assert
        Assert.Equal((1, 0, -2, 0), Place(next));
    }

    [Fact]
    public void Next_BlockedByAWall_ChangesNeitherHisPictureNorTheSideFireDigs()
    {
        // Arrange
        var state = State("""
            #P
            ##
            """, digSide: Direction.Right);

        // Act
        var next = state.Next(Joystick.Left);

        // Assert
        Assert.Equal(state.Player, next.Player);
    }

    [Fact]
    public void Next_WalkingOffALadder_CentresHimUpAndDownOnTheWay()
    {
        // Arrange
        var state = State("""

            P
            ##
            """, offsetY: -2, on: Tile.Ladder);

        // Act
        var places = Run(state, Joystick.Right, passes: 2).Select(Place);

        // Assert
        Assert.Equal([(0, 1, 1, -1), (0, 1, 2, 0)], places);
    }

    [Theory]
    [InlineData(Joystick.Left, Direction.Left)]
    [InlineData(Joystick.Right, Direction.Right)]
    public void Next_Walking_ShowsHimRunningAndSetsTheSideFireDigs(Joystick joystick, Direction side)
    {
        // Arrange
        var state = State("""
            P
            ###
            """, column: 1, pose: 0x10);

        // Act
        var player = state.Next(joystick).Player;

        // Assert
        Assert.Equal((Pose.Run, side, side), (player.Pose, player.Facing, player.DigSide));
    }

    [Fact]
    public void Next_NothingHeld_StaysTheSame()
    {
        // Arrange
        var state = State("""
            P
            #
            """, offsetX: 1);

        // Act
        var next = state.Next(Joystick.None);

        // Assert
        Assert.Equal(state.Player, next.Player);
    }

    // Falling

    [Fact]
    public void Next_NothingBelowHim_FallsFivePassesToTheNextTileWhateverIsHeld()
    {
        // Arrange
        var state = State("""

            P


            #
            """);

        // Act
        var states = Run(state, Joystick.Right | Joystick.Fire, passes: 5);

        // Assert
        Assert.Equal([(0, 1, 0, 1), (0, 1, 0, 2), (0, 2, 0, -2), (0, 2, 0, -1), (0, 2, 0, 0)], states.Select(Place));
        Assert.All(states, next => Assert.True(next.Player.Falling));
        Assert.All(states, next => Assert.Equal(Pose.Fall, next.Player.Pose));
    }

    [Fact]
    public void Next_WalkingOffAnEdge_FallsFromTheNextTilesSideAndIsCentredOnTheWay()
    {
        // Arrange
        var state = State("""
            P
            #


            """, offsetX: 2);

        // Act
        var places = Run(state, Joystick.Right, passes: 3).Select(Place);

        // Assert
        Assert.Equal([(1, 0, -2, 0), (1, 0, -1, 1), (1, 0, 0, 2)], places);
    }

    [Theory]
    [InlineData('#')]
    [InlineData('@')]
    [InlineData('H')]
    public void Next_SomethingToStandOnBelowHim_StopsHisFallInTheMiddleOfHisTile(char below)
    {
        // Arrange
        var state = State($"""

            P
            {below}
            """, offsetY: -1, falling: true);

        // Act
        var states = Run(state, Joystick.None, passes: 2);

        // Assert
        Assert.Equal([(0, 1, 0, 0), (0, 1, 0, 0)], states.Select(Place));
        Assert.Equal([true, false], states.Select(next => next.Player.Falling));
    }

    [Theory]
    [InlineData(' ')]
    [InlineData('T')]
    [InlineData('-')]
    [InlineData('$')]
    [InlineData('h')]
    [InlineData('o')]
    public void Next_NothingToStandOnBelowHim_FallsOn(char below)
    {
        // Arrange
        var state = State($"""
            P
            {below}

            """);

        // Act
        var next = state.Next(Joystick.None);

        // Assert
        Assert.Equal((0, 0, 0, 1), Place(next));
        Assert.True(next.Player.Falling);
    }

    [Fact]
    public void Next_OnTheBottomRow_StandsThere()
    {
        // Arrange
        var state = State(new string('\n', 15) + "P");

        // Act
        var next = state.Next(Joystick.Down);

        // Assert
        Assert.Equal((0, 15, 0, 0), Place(next));
        Assert.False(next.Player.Falling);
    }

    [Theory]
    [InlineData(Direction.Left)]
    [InlineData(Direction.Right)]
    public void Next_Falling_FacesTheSideOfHisLastMove(Direction side)
    {
        // Arrange
        var state = State("P", pose: 0x10, digSide: side);

        // Act
        var player = state.Next(Joystick.None).Player;

        // Assert
        Assert.Equal((Pose.Fall, side), (player.Pose, player.Facing));
    }

    // Ropes

    [Fact]
    public void Next_FallingPastARope_IsCaughtInTheMiddleOfIt()
    {
        // Arrange
        var state = State("""

            P


            """, offsetY: -1, on: Tile.Rope, falling: true);

        // Act
        var states = Run(state, Joystick.None, passes: 2);

        // Assert
        Assert.Equal([(0, 1, 0, 0), (0, 1, 0, 0)], states.Select(Place));
        Assert.Equal([true, false], states.Select(next => next.Player.Falling));
    }

    [Fact]
    public void Next_MovingAlongARope_HangsFromIt()
    {
        // Arrange
        var state = State("""
            P--


            """, on: Tile.Rope);

        // Act
        var states = Run(state, Joystick.Right, passes: 5);

        // Assert
        Assert.Equal((1, 0, 0, 0), Place(states[^1]));
        Assert.All(states, next => Assert.Equal((Pose.Rope, false), (next.Player.Pose, next.Player.Falling)));
    }

    [Fact]
    public void Next_MovingOffTheEndOfARope_Falls()
    {
        // Arrange
        var state = State("""
            P


            """, on: Tile.Rope, offsetX: 2);

        // Act
        var places = Run(state, Joystick.Right, passes: 2).Select(Place);

        // Assert
        Assert.Equal([(1, 0, -2, 0), (1, 0, -1, 1)], places);
    }

    [Fact]
    public void Next_DownOnARope_LetsGoOfIt()
    {
        // Arrange
        var state = State("""

            P


            """, on: Tile.Rope);

        // Act
        var states = Run(state, Joystick.Down, passes: 2);

        // Assert
        Assert.Equal([(0, 1, 0, 1), (0, 1, 0, 2)], states.Select(Place));
        Assert.Equal([Pose.Ladder, Pose.Fall], states.Select(next => next.Player.Pose));
    }

    [Fact]
    public void Next_UpOnARope_DoesNothing()
    {
        // Arrange
        var state = State("""
            H
            P

            """, on: Tile.Rope);

        // Act
        var next = state.Next(Joystick.Up);

        // Assert
        Assert.Equal(state.Player, next.Player);
    }

    // Ladders

    [Theory]
    [InlineData(Joystick.Up, -1)]
    [InlineData(Joystick.Down, 1)]
    public void Next_OnALadder_TakesFivePassesToTheNextTile(Joystick joystick, int step)
    {
        // Arrange
        var state = State("""
            H
            H
            P
            H
            H
            """, on: Tile.Ladder);

        // Act
        var states = Run(state, joystick, passes: 5);

        // Assert
        Assert.Equal(
            [
                (0, 2, 0, step), (0, 2, 0, 2 * step), (0, 2 + step, 0, -2 * step), (0, 2 + step, 0, -step),
                (0, 2 + step, 0, 0),
            ],
            states.Select(Place));
        Assert.All(states, next => Assert.Equal((Pose.Ladder, null), (next.Player.Pose, next.Player.Facing)));
    }

    [Theory]
    [InlineData(Joystick.Up)]
    [InlineData(Joystick.Down)]
    public void Next_Climbing_CentresHimLeftAndRightOnTheWay(Joystick joystick)
    {
        // Arrange
        var state = State("""
            H
            P
            H
            """, on: Tile.Ladder, offsetX: 2);

        // Act
        var states = Run(state, joystick, passes: 2);

        // Assert
        Assert.Equal([1, 0], states.Select(next => next.Player.OffsetX));
    }

    [Fact]
    public void Next_ClimbingOffTheTopOfALadder_StandsOnIt()
    {
        // Arrange
        var state = State("""


            P
            #
            """, on: Tile.Ladder);

        // Act
        var states = Run(state, Joystick.Up, passes: 7);

        // Assert
        Assert.Equal(
            [(0, 2, 0, -1), (0, 2, 0, -2), (0, 1, 0, 2), (0, 1, 0, 1), (0, 1, 0, 0), (0, 1, 0, 0), (0, 1, 0, 0)],
            states.Select(Place));
        Assert.All(states, next => Assert.False(next.Player.Falling));
    }

    [Theory]
    [InlineData('#')]
    [InlineData('@')]
    [InlineData('T')]
    public void Next_WallAboveTheLadder_StopsHimInTheMiddleOfHisTile(char above)
    {
        // Arrange
        var state = State($"""
            {above}
            P
            #
            """, on: Tile.Ladder, offsetY: 1);

        // Act
        var places = Run(state, Joystick.Up, passes: 2).Select(Place);

        // Assert
        Assert.Equal([(0, 1, 0, 0), (0, 1, 0, 0)], places);
    }

    [Fact]
    public void Next_UpOnTheTopRow_StopsHimInTheMiddleOfHisTile()
    {
        // Arrange
        var state = State("""
            P
            H
            """, on: Tile.Ladder);

        // Act
        var next = state.Next(Joystick.Up);

        // Assert
        Assert.Equal((0, 0, 0, 0), Place(next));
    }

    [Theory]
    [InlineData('#')]
    [InlineData('@')]
    public void Next_FloorBelowTheLadder_StopsHimInTheMiddleOfHisTile(char below)
    {
        // Arrange
        var state = State($"""

            P
            {below}
            """, on: Tile.Ladder, offsetY: -1);

        // Act
        var places = Run(state, Joystick.Down, passes: 2).Select(Place);

        // Assert
        Assert.Equal([(0, 1, 0, 0), (0, 1, 0, 0)], places);
    }

    [Theory]
    [InlineData('T')]
    [InlineData(' ')]
    [InlineData('o')]
    public void Next_NoFloorBelowTheLadder_LetsHimClimbOffIt(char below)
    {
        // Arrange
        var state = State($"""

            P
            {below}

            """, on: Tile.Ladder);

        // Act
        var next = state.Next(Joystick.Down);

        // Assert
        Assert.Equal((0, 1, 0, 1), Place(next));
    }

    [Fact]
    public void Next_DownFromTheTopOfALadder_ClimbsDownIt()
    {
        // Arrange
        var state = State("""

            P
            H
            """);

        // Act
        var states = Run(state, Joystick.Down, passes: 3);

        // Assert
        Assert.Equal([(0, 1, 0, 1), (0, 1, 0, 2), (0, 2, 0, -2)], states.Select(Place));
        Assert.All(states, next => Assert.False(next.Player.Falling));
    }

    [Fact]
    public void Next_UpWithoutALadderBelow_DoesNothing()
    {
        // Arrange: a little above the floor, as after a hole closed right below him
        var state = State("""

            P
            #
            """, offsetY: 1);

        // Act
        var next = state.Next(Joystick.Up);

        // Assert
        Assert.Equal(state.Player, next.Player);
    }

    // Diagonals and fire

    [Theory]
    [InlineData(Joystick.Up)]
    [InlineData(Joystick.Down)]
    public void Next_HeldDiagonallyWhereHeCanClimb_Climbs(Joystick upOrDown)
    {
        // Arrange
        var state = State("""
            HHH
            HPH
            HHH
            """, on: Tile.Ladder);

        // Act
        var next = state.Next(upOrDown | Joystick.Left);

        // Assert
        Assert.Equal((1, 1, 0, upOrDown == Joystick.Up ? -1 : 1), Place(next));
    }

    [Theory]
    [InlineData(Joystick.Up)]
    [InlineData(Joystick.Down)]
    public void Next_HeldDiagonallyWhereHeCannotClimb_MovesToTheSide(Joystick upOrDown)
    {
        // Arrange
        var state = State("""

             P
            ###
            """);

        // Act
        var next = state.Next(upOrDown | Joystick.Left);

        // Assert
        Assert.Equal((1, 1, -1, 0), Place(next));
    }

    [Theory]
    [InlineData("  H", "#P#", "###")]
    [InlineData("  H", "@P@", "@@@")]
    public void Next_FireWhereHeCannotDig_DoesNothingAlsoWithADirectionHeld(string above, string row, string below)
    {
        // Arrange
        var state = State($"{above}\n{row}\n{below}", on: Tile.Ladder);

        // Act
        var next = state.Next(Joystick.Fire | Joystick.Up);

        // Assert
        Assert.Equal(state.Player, next.Player);
    }

    // Digging

    [Theory]
    [InlineData(Direction.Left, 0)]
    [InlineData(Direction.Right, 2)]
    public void Next_Fire_DigsTheBrickBelowBesideHimIn13Passes(Direction side, int holeColumn)
    {
        // Arrange
        var state = State("""

             P
            ###
            """, digSide: side);

        // Act
        var first = state.Next(Joystick.Fire);
        var rest = Run(first, Joystick.None, passes: 12);

        // Assert
        var dig = new[] { first }.Concat(rest).ToList();
        Assert.Equal(Enumerable.Range(1, 12).Append(0), dig.Select(next => next.Player.DigPasses));
        Assert.All(dig.SkipLast(1), next => Assert.Equal(side, next.Player.Digging));
        Assert.All(dig.SkipLast(1), next => Assert.Empty(next.Holes));
        Assert.Null(dig[^1].Player.Digging);
        Assert.Equal([new Hole(new TilePosition(holeColumn, 2), Hole.OpenPasses - 1)], dig[^1].Holes);
    }

    [Fact]
    public void Next_Digging_ShowsHimDiggingForSixPasses()
    {
        // Arrange
        var state = State("""

            P
            ##
            """, pose: 0x10);

        // Act
        var players = Run(state, Joystick.Fire, passes: 13).Select(next => next.Player).ToList();

        // Assert
        Assert.Equal(
            Enumerable.Repeat(Pose.Dig, 6).Concat(Enumerable.Repeat(Pose.Run, 7)),
            players.Select(player => player.Pose));
        Assert.All(players, player => Assert.Equal(Direction.Right, player.Facing));
    }

    [Fact]
    public void Next_Digging_GoesOnWhateverIsHeld()
    {
        // Arrange
        var state = State("""

             P
            ###
            """, digging: Direction.Right, digPasses: 3);

        // Act
        var next = state.Next(Joystick.Left);

        // Assert
        Assert.Equal((1, 1, 0, 0), Place(next));
        Assert.Equal((Direction.Right, 4), (next.Player.Digging, next.Player.DigPasses));
    }

    [Fact]
    public void Next_Digging_CentresHim()
    {
        // Arrange
        var state = State("""

            P
            ##
            """, offsetX: -2, offsetY: 1, on: Tile.Ladder);

        // Act
        var places = Run(state, Joystick.Fire, passes: 3).Select(Place);

        // Assert
        Assert.Equal([(0, 1, -1, 0), (0, 1, 0, 0), (0, 1, 0, 0)], places);
    }

    [Theory]
    [InlineData('@')]
    [InlineData('T')]
    [InlineData('H')]
    [InlineData(' ')]
    [InlineData('o')]
    public void Next_NoBrickBelowBesideHim_CannotDig(char below)
    {
        // Arrange
        var state = State($"""

            P
            #{below}
            """);

        // Act
        var next = state.Next(Joystick.Fire);

        // Assert
        Assert.Equal(state.Player, next.Player);
    }

    [Theory]
    [InlineData('#')]
    [InlineData('@')]
    [InlineData('T')]
    [InlineData('H')]
    [InlineData('-')]
    [InlineData('$')]
    public void Next_SomethingBesideHim_CannotDig(char beside)
    {
        // Arrange
        var state = State($"""

            P{beside}
            ##
            """);

        // Act
        var next = state.Next(Joystick.Fire);

        // Assert
        Assert.Equal(state.Player, next.Player);
    }

    [Theory]
    [InlineData('h')] // a hidden ladder is an empty tile until it appears
    [InlineData('o')]
    public void Next_HiddenLadderOrHoleBesideHim_CanDig(char beside)
    {
        // Arrange
        var state = State($"""

            P{beside}
            ##
            """);

        // Act
        var next = state.Next(Joystick.Fire);

        // Assert
        Assert.Equal(Direction.Right, next.Player.Digging);
    }

    [Fact]
    public void Next_OnTheBottomRow_CannotDig()
    {
        // Arrange
        var state = State(new string('\n', 15) + "P");

        // Act
        var next = state.Next(Joystick.Fire);

        // Assert
        Assert.Null(next.Player.Digging);
    }

    [Theory]
    [InlineData(0, Direction.Left)]
    [InlineData(27, Direction.Right)]
    public void Next_FireAtTheLevelsSide_CannotDig(int column, Direction side)
    {
        // Arrange
        var state = State("\nP\n" + new string('#', 28), column: column, digSide: side);

        // Act
        var next = state.Next(Joystick.Fire);

        // Assert
        Assert.Null(next.Player.Digging);
    }

    [Theory]
    [InlineData(Tile.Rope)]
    [InlineData(Tile.Ladder)]
    public void Next_FireHangingAtTheEndOfARopeOrOnALadder_Digs(Tile on)
    {
        // Arrange: nothing below him
        var state = State("""

            P
             #
            """, on: on);

        // Act
        var last = Run(state, Joystick.Fire, passes: 13)[^1];

        // Assert
        Assert.Equal((0, 1, 0, 0), Place(last));
        Assert.Equal([new TilePosition(1, 2)], last.Holes.Select(hole => hole.Position));
    }

    [Fact]
    public void Next_SomethingComesBesideHimWhileHeDigs_StopsTheDig()
    {
        // Arrange: a dig well under way, with a ladder that has just appeared beside him
        var state = State("""

            PH
            ##
            """, digging: Direction.Right, digPasses: 12);

        // Act
        var next = state.Next(Joystick.Fire);

        // Assert
        Assert.Equal((null, 0), (next.Player.Digging, next.Player.DigPasses));
        Assert.Empty(next.Holes);
    }

    // Holes

    [Fact]
    public void Next_Hole_CountsDownAndCloses()
    {
        // Arrange
        var state = State("""

            P
            # o
            """, holePasses: 2);

        // Act
        var states = Run(state, Joystick.None, passes: 2);

        // Assert
        Assert.Equal([1], states[0].Holes.Select(hole => hole.PassesLeft));
        Assert.Empty(states[1].Holes);
        Assert.Equal(Tile.Brick, states[1].Level[2, 2]);
        Assert.True(states[1].Alive);
    }

    [Fact]
    public void Next_HoleClosesOnHim_KillsHim()
    {
        // Arrange
        var state = State("""

            P
            """, on: Tile.Brick, standsInHole: true, holePasses: 1);

        // Act
        var next = state.Next(Joystick.None);

        // Assert
        Assert.False(next.Alive);
        Assert.Empty(next.Holes);
    }

    [Fact]
    public void Next_HoleClosesBelowHim_HoldsHimInTheNextPass()
    {
        // Arrange: he has just come over the hole
        var state = State("""
             P
            #o#
            ###
            """, offsetX: -2, holePasses: 1);

        // Act
        var places = Run(state, Joystick.Right, passes: 2).Select(Place);

        // Assert: one pass of falling, and then the brick is back, below him
        Assert.Equal([(1, 0, -1, 1), (1, 0, 0, 0)], places);
    }

    [Fact]
    public void Next_InAHole_FallsOutOfItsBottomIfThereIsNothingBelow()
    {
        // Arrange
        var state = State("""
            P


            """, on: Tile.Brick, standsInHole: true);

        // Act
        var next = state.Next(Joystick.None);

        // Assert
        Assert.Equal((0, 0, 0, 1), Place(next));
    }

    [Fact]
    public void Next_WalkingBetweenTwoHolesNextToEachOther_IsPossible()
    {
        // Arrange
        var state = State("""

            #Po#
            ####
            """, on: Tile.Brick, standsInHole: true);

        // Act
        var places = Run(state, Joystick.Right, passes: 5).Select(Place);

        // Assert
        Assert.Equal((2, 1, 0, 0), places.Last());
    }

    // Gold

    [Fact]
    public void Next_GoldReached_IsTakenInTheMiddleOfItsTile()
    {
        // Arrange
        var state = State("""

            P$
            ##
            """, goldLeft: 2);

        // Act
        var states = Run(state, Joystick.Right, passes: 5);

        // Assert
        Assert.Equal([2, 2, 2, 2, 1], states.Select(next => next.GoldLeft));
        Assert.Equal([0, 0, 0, 0, 250], states.Select(next => next.Score));
        Assert.Equal(Tile.Gold, states[3].Level[1, 1]);
        Assert.Equal(Tile.Empty, states[4].Level[1, 1]);
    }

    [Fact]
    public void Next_FallingThroughGold_TakesIt()
    {
        // Arrange
        var state = State("""
            P


            """, on: Tile.Gold, offsetY: -1, goldLeft: 2);

        // Act
        var next = state.Next(Joystick.None);

        // Assert
        Assert.Equal(1, next.GoldLeft);
        Assert.True(next.Player.Falling);
    }

    [Fact]
    public void Next_FallingIntoGold_TakesItOnlyInTheMiddleOfItsTile()
    {
        // Arrange
        var state = State("""
            P


            """, on: Tile.Gold, offsetY: -2, falling: true);

        // Act
        var states = Run(state, Joystick.None, passes: 2);

        // Assert
        Assert.Equal([9, 8], states.Select(next => next.GoldLeft));
    }

    [Fact]
    public void Next_ClimbingUpToGold_TakesIt()
    {
        // Arrange
        var state = State("""

            $
            P
            """, on: Tile.Ladder);

        // Act
        var states = Run(state, Joystick.Up, passes: 5);

        // Assert
        Assert.Equal([9, 9, 9, 9, 8], states.Select(next => next.GoldLeft));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    public void Next_CentredOnGoldByADig_TakesIt(int offsetX, int offsetY)
    {
        // Arrange
        var state = State("""

            P
            H#
            """, on: Tile.Gold, offsetX: offsetX, offsetY: offsetY);

        // Act
        var next = state.Next(Joystick.Fire);

        // Assert
        Assert.Equal(8, next.GoldLeft);
        Assert.Equal(Direction.Right, next.Player.Digging);
    }

    // The exit

    [Fact]
    public void Next_LastGoldTaken_ShowsTheExitLaddersInTheSamePass()
    {
        // Arrange
        var state = State("""
              h
              h
            P
            ###
            """, on: Tile.Gold, offsetX: -1, goldLeft: 1);

        // Act
        var next = state.Next(Joystick.Right);

        // Assert
        Assert.Equal(0, next.GoldLeft);
        Assert.Equal([Tile.Ladder, Tile.Ladder], [next.Level[2, 0], next.Level[2, 1]]);
        Assert.Empty(next.Level.HiddenLadders);
    }

    [Fact]
    public void Next_GoldLeft_KeepsTheExitLaddersHidden()
    {
        // Arrange
        var state = State("""
              h
            P
            ###
            """, on: Tile.Gold, offsetX: -1, goldLeft: 2);

        // Act
        var next = state.Next(Joystick.Right);

        // Assert
        Assert.Equal(Tile.Empty, next.Level[2, 0]);
        Assert.Equal([new TilePosition(2, 0)], next.Level.HiddenLadders);
    }

    [Theory]
    [InlineData(0, 1, true)]
    [InlineData(1, 1, false)] // gold left
    [InlineData(0, 2, false)] // not in the middle of the top row
    public void Next_ReachingTheTopRow_CompletesTheLevelIfNoGoldIsLeft(int goldLeft, int offsetY, bool completed)
    {
        // Arrange
        var state = State("""
            P
            H
            """, on: Tile.Ladder, offsetY: offsetY, goldLeft: goldLeft);

        // Act
        var next = state.Next(Joystick.Up);

        // Assert
        Assert.Equal(completed, next.Completed);
    }

    // The state

    [Fact]
    public void Next_PlayerDead_GivesTheSameState()
    {
        // Arrange
        var state = State("""

            P
            """, on: Tile.Brick, standsInHole: true, holePasses: 1).Next(Joystick.None);

        // Act
        var next = state.Next(Joystick.Right);

        // Assert
        Assert.Same(state, next);
    }

    [Fact]
    public void Next_LevelCompleted_GivesTheSameState()
    {
        // Arrange
        var state = State("""
            P
            H
            """, on: Tile.Ladder, goldLeft: 0).Next(Joystick.None);

        // Act
        var next = state.Next(Joystick.Down);

        // Assert
        Assert.Same(state, next);
    }

    [Fact]
    public void Next_Guards_AreNotSupported()
    {
        // Arrange
        var state = State("""
            P G
            ###
            """);

        // Act
        var exception = Record.Exception(() => state.Next(Joystick.None));

        // Assert
        Assert.IsType<NotSupportedException>(exception);
    }

    [Fact]
    public void Next_LeavesTheStateAsItWas()
    {
        // Arrange
        var state = State("""

            P$ o
            ####
            """, offsetX: 2, goldLeft: 1);
        var (player, level, holes) = (state.Player, state.Level.ToString(), state.Holes.ToList());

        // Act
        Run(state, Joystick.Right, passes: 10);

        // Assert
        Assert.Equal(player, state.Player);
        Assert.Equal(level, state.Level.ToString());
        Assert.Equal(holes, state.Holes);
        Assert.Equal((1, 0), (state.GoldLeft, state.Score));
    }

    // The states after each of the passes.
    private static List<GameState> Run(GameState state, Joystick joystick, int passes)
    {
        var states = new List<GameState>();
        for (var pass = 0; pass < passes; pass++)
        {
            state = state.Next(joystick);
            states.Add(state);
        }

        return states;
    }

    private static (int Column, int Row, int OffsetX, int OffsetY) Place(GameState state) =>
        (state.Player.Position.Column, state.Player.Position.Row, state.Player.OffsetX, state.Player.OffsetY);

    // A state from text as Level.Parse reads it, with o for a hole. The player stands at the P, or in that row at the
    // column given, and on the tile given. Read from RAM laid out as the game has it. There's plenty of gold left,
    // unless something else is given, so that he isn't done just by standing on the top row.
    private static GameState State(
        string text,
        int? column = null,
        int offsetX = 0,
        int offsetY = 0,
        Tile on = Tile.Empty,
        byte pose = 0x08,
        bool falling = false,
        Direction digSide = Direction.Right,
        Direction? digging = null,
        int digPasses = 0,
        bool standsInHole = false,
        int holePasses = 100,
        int goldLeft = 9)
    {
        var ram = new byte[0x10000];
        var holes = new List<TilePosition>();
        var player = default(TilePosition);
        var hiddenLadders = 0;
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        for (var row = 0; row < lines.Length; row++)
            for (var c = 0; c < lines[row].Length; c++)
                ram[RamMap.TileAddress(RamMap.LevelAsBuilt, c, row)] = (byte)ReadTile(lines[row][c], new(c, row));

        if (column is { } given)
            player = player with { Column = given };
        ram[RamMap.TileAddress(RamMap.LevelAsBuilt, player.Column, player.Row)] = (byte)on;
        if (standsInHole)
            holes.Add(player);

        (ram[RamMap.PlayerColumn], ram[RamMap.PlayerRow]) = ((byte)player.Column, (byte)player.Row);
        (ram[RamMap.PlayerStepX], ram[RamMap.PlayerStepY]) = ((byte)(offsetX + 2), (byte)(offsetY + 2));
        ram[RamMap.PlayerSprite] = pose;
        ram[RamMap.PlayerAlive] = 1;
        ram[RamMap.PlayerFalling] = (byte)(falling ? 0x00 : 0x20);
        ram[RamMap.PlayerLastSide] = Side(digSide);
        ram[RamMap.PlayerDigging] = digging is { } side ? Side(side) : (byte)0;
        ram[RamMap.PlayerDigStep] = (byte)(digPasses + (digging == Direction.Right ? RamMap.DigStepsRight : 0));
        for (var slot = 0; slot < holes.Count; slot++)
        {
            (ram[RamMap.HoleColumns + slot], ram[RamMap.HoleRows + slot]) =
                ((byte)holes[slot].Column, (byte)holes[slot].Row);
            ram[RamMap.HoleTimers + slot] = (byte)holePasses;
        }

        ram[RamMap.GoldLeft] = (byte)goldLeft;
        (ram[RamMap.MainCodePosition], ram[RamMap.MainCodePosition + 1]) = (0x55, 0x61);
        Assert.True(GameState.TryRead(ram, out var state));
        return state;

        Tile ReadTile(char character, TilePosition position)
        {
            switch (character)
            {
                case 'P':
                    player = position;
                    return Tile.Empty;
                case 'o':
                    holes.Add(position);
                    return Tile.Brick;
                case 'h':
                    hiddenLadders = ++ram[RamMap.HiddenLadderCount];
                    (ram[RamMap.HiddenLadderColumns + hiddenLadders], ram[RamMap.HiddenLadderRows + hiddenLadders]) =
                        ((byte)position.Column, (byte)position.Row);
                    return Tile.Empty;
                case 'G':
                    var guard = ++ram[RamMap.GuardCount];
                    (ram[RamMap.GuardColumns + guard], ram[RamMap.GuardRows + guard]) =
                        ((byte)position.Column, (byte)position.Row);
                    (ram[RamMap.GuardStepsX + guard], ram[RamMap.GuardStepsY + guard]) = (2, 2);
                    return Tile.Empty;
                default:
                    return Level.Parse(1, character.ToString())[0, 0];
            }
        }

        static byte Side(Direction side) => (byte)(side == Direction.Left ? 0xFF : 0x01);
    }
}
