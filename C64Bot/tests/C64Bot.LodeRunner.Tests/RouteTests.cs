using C64Bot.Libretro;
using static C64Bot.LodeRunner.Tests.TestStates;

namespace C64Bot.LodeRunner.Tests;

public class RouteTests
{
    [Fact]
    public void Find_TwoGoldOnTheFloor_GoesToTheNearer()
    {
        // Arrange
        var state = State("""
            $ P  $
            @@@@@@
            """);

        // Act
        var route = Route.Find(state);

        // Assert
        Assert.NotNull(route);
        Assert.Equal(Enumerable.Repeat(Joystick.Left, 10), route.Passes);
        Assert.Equal((0, 0, 0, 0), Place(route.End));
        Assert.Equal(8, route.End.GoldLeft);
    }

    [Fact]
    public void Find_GoldInAPocket_DigsDownToIt()
    {
        // Arrange
        var state = State("""
            P
            @#@
            @$@
            @@@
            """);

        // Act
        var route = Route.Find(state);

        // Assert
        Assert.NotNull(route);
        Assert.Equal([MoveKind.DigRight, MoveKind.Right], route.Moves.Select(move => move.Kind));
        Assert.Equal((1, 2, 0, 0), Place(route.End));
        Assert.Equal(8, route.End.GoldLeft);
    }

    [Fact]
    public void Find_GoldThreeBricksDown_DigsAPitLayerByLayer()
    {
        // Arrange
        var state = State("""
             P
            ############################
            ############################
            ############################
            @@@@$@@@@@@@@@@@@@@@@@@@@@@@
            @@@@@@@@@@@@@@@@@@@@@@@@@@@@
            """);

        // Act
        var route = Route.Find(state);

        // Assert
        Assert.NotNull(route);
        Assert.Equal((4, 4, 0, 0), Place(route.End));
        Assert.Equal(8, route.End.GoldLeft);
        Assert.Equal(
            [new(4, 1), new(3, 1), new(2, 1), new(4, 2), new(3, 2), new(4, 3)], HolesDug(state, route));
    }

    [Fact]
    public void Find_GoldThreeBricksDownWithASolidBlockInTheWay_DigsTheSecondLayerTheOtherWay()
    {
        // Arrange
        var state = State("""
             P
            ############################
            ###@########################
            ############################
            @@@@$@@@@@@@@@@@@@@@@@@@@@@@
            @@@@@@@@@@@@@@@@@@@@@@@@@@@@
            """);

        // Act
        var route = Route.Find(state);

        // Assert
        Assert.NotNull(route);
        Assert.Equal((4, 4, 0, 0), Place(route.End));
        var holes = HolesDug(state, route);
        Assert.Equal([1, 1, 1, 2, 2, 3], holes.Select(hole => hole.Row));
        Assert.Equal([new(4, 2), new(5, 2)], holes.Where(hole => hole.Row == 2));
    }

    [Fact]
    public void Find_GoldFourBricksDown_DigsAPitLayerByLayerEachLayerInARow()
    {
        // Arrange
        var state = State("""
             P
            ############################
            ############################
            ############################
            ############################
            @@@@@$@@@@@@@@@@@@@@@@@@@@@@
            @@@@@@@@@@@@@@@@@@@@@@@@@@@@
            """);

        // Act
        var route = Route.Find(state);

        // Assert
        Assert.NotNull(route);
        Assert.Equal((5, 5, 0, 0), Place(route.End));
        Assert.Equal(8, route.End.GoldLeft);
        var holes = HolesDug(state, route);
        Assert.Equal([1, 1, 1, 1, 2, 2, 2, 3, 3, 4], holes.Select(hole => hole.Row));
        foreach (var layer in holes.GroupBy(hole => hole.Row))
        {
            var columns = layer.Select(hole => hole.Column).ToList();
            var steps = columns.Zip(columns.Skip(1), (one, next) => next - one).ToList();
            Assert.True(steps.All(step => step == 1) || steps.All(step => step == -1), $"Layer {layer.Key}: {string.Join(", ", columns)}");
        }
    }

    [Fact]
    public void Find_BrickBelowAHoleDugLater_DigsItFromWhereHeWasBefore()
    {
        // Arrange
        var state = State("""
            H-  @
            HP#@@
            @@#@@
            @@$@@
            @@@@@
            """);

        // Act
        var route = Route.Find(state);

        // Assert
        Assert.NotNull(route);
        Assert.Equal((2, 3, 0, 0), Place(route.End));
        Assert.Equal([new(2, 1), new(2, 2)], HolesDug(state, route));
    }

    [Fact]
    public void Find_GoldFourBricksDownBesideALadder_DigsStraightDownFromTheLadder()
    {
        // Arrange
        var state = State("""
             P
            @H##########################
            @H##########################
            @H##########################
            @@##########################
            @@$@@@@@@@@@@@@@@@@@@@@@@@@@
            @@@@@@@@@@@@@@@@@@@@@@@@@@@@
            """);

        // Act
        var route = Route.Find(state);

        // Assert
        Assert.NotNull(route);
        Assert.Equal((2, 5, 0, 0), Place(route.End));
        Assert.Equal([new(2, 1), new(2, 2), new(2, 3), new(2, 4)], HolesDug(state, route));
        Assert.Equal(80, route.Passes.Count);
    }

    [Fact]
    public void Find_GoldBelowTheEndOfARope_DigsFromTheRope()
    {
        // Arrange
        var state = State("""
            P- @
              #@
            @@$@
            @@@@
            """, on: Tile.Rope);

        // Act
        var route = Route.Find(state);

        // Assert
        Assert.NotNull(route);
        Assert.Equal([MoveKind.Right, MoveKind.DigRight, MoveKind.Right], route.Moves.Select(move => move.Kind));
        Assert.Equal(Tile.Rope, state.Level[route.Moves[0].End.Player.Position]);
        Assert.Equal((2, 2, 0, 0), Place(route.End));
    }

    [Fact]
    public void Find_HoleInTheWay_WaitsForItToCloseAndWalksOverIt()
    {
        // Arrange
        var state = State("""
            P  $
            #o##
            @@@@
            """, holePasses: 40);

        // Act
        var route = Route.Find(state);

        // Assert
        Assert.NotNull(route);
        Assert.Equal(
            [MoveKind.WaitForHole, MoveKind.Right, MoveKind.Right, MoveKind.Right],
            route.Moves.Select(move => move.Kind));
        Assert.Equal(55, route.Passes.Count);
        Assert.Equal((3, 0, 0, 0), Place(route.End));
    }

    [Fact]
    public void Find_NoGoldLeft_ClimbsToTheTopRowAndCompletesTheLevel()
    {
        // Arrange
        var state = State("""
              H
              H
            P H
            @@@@
            """, goldLeft: 0);

        // Act
        var route = Route.Find(state);

        // Assert
        Assert.NotNull(route);
        Assert.True(route.End.Completed);
        Assert.Equal((2, 0, 0, 0), Place(route.End));
        Assert.Equal(20, route.Passes.Count);
    }

    [Fact]
    public void Find_GoldBehindASolidBlock_GivesNone()
    {
        // Arrange
        var state = State("""
            P  @$
            @@@@@
            """);

        // Act
        var route = Route.Find(state);

        // Assert
        Assert.Null(route);
    }

    [Fact]
    public void Find_GoldAboveHimWithNoLadder_GivesNone()
    {
        // Arrange
        var state = State("""
             $
            @@@
            #P#
            ###
            """);

        // Act
        var route = Route.Find(state);

        // Assert
        Assert.Null(route);
    }

    [Fact]
    public void Find_GoldUnderABrickUnderASolidBlock_GivesNone()
    {
        // Arrange
        var state = State("""
             P
            ############################
            ###@@#######################
            ############################
            @@@@$@@@@@@@@@@@@@@@@@@@@@@@
            @@@@@@@@@@@@@@@@@@@@@@@@@@@@
            """);

        // Act
        var route = Route.Find(state);

        // Assert
        Assert.Null(route);
    }

    [Fact]
    public void Find_GoldUnderABrickHeCantDig_GivesNone()
    {
        // Arrange
        var state = State("""
            P#
            @#
            @$
            @@
            """);

        // Act
        var route = Route.Find(state);

        // Assert
        Assert.Null(route);
    }

    [Fact]
    public void Find_HeldPassByPass_TakesHimToTheEnd()
    {
        // Arrange
        var state = State("""
             P
            ############################
            ############################
            ############################
            @@@@$@@@@@@@@@@@@@@@@@@@@@@@
            @@@@@@@@@@@@@@@@@@@@@@@@@@@@
            """);
        var route = Route.Find(state);
        Assert.NotNull(route);

        // Act
        var end = route.Passes.Aggregate(state, (before, joystick) => before.Next(joystick));

        // Assert
        Assert.Equal(Describe(route.End), Describe(end));
        Assert.Equal(route.Moves.Sum(move => move.Passes.Count), route.Passes.Count);
    }

    [Fact]
    public void Find_WhileDigging_Throws()
    {
        // Arrange
        var state = State("""
            P  $
            ####
            """, digging: Direction.Right, digPasses: 3);

        // Act
        var exception = Record.Exception(() => Route.Find(state));

        // Assert
        Assert.IsType<ArgumentException>(exception);
    }

    // The holes he digs on the route, one after another.
    private static List<TilePosition> HolesDug(GameState start, Route route)
    {
        var holes = new List<TilePosition>();
        var before = start;
        foreach (var move in route.Moves)
        {
            holes.AddRange(move.End.Holes.Select(hole => hole.Position).Except(before.Holes.Select(hole => hole.Position)));
            before = move.End;
        }

        return holes;
    }

    private static (int Column, int Row, int OffsetX, int OffsetY) Place(GameState state) =>
        (state.Player.Position.Column, state.Player.Position.Row, state.Player.OffsetX, state.Player.OffsetY);

    private static string Describe(GameState state) =>
        $"{state.Player}, gold {state.GoldLeft}, alive {state.Alive}, completed {state.Completed}, holes "
        + string.Join(' ', state.Holes) + $"\n{state.Level}";
}
