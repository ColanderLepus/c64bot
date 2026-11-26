using C64Bot.Libretro;
using static C64Bot.LodeRunner.Tests.TestStates;

namespace C64Bot.LodeRunner.Tests;

public class LevelGraphTests
{
    // Places and runs

    [Fact]
    public void Places_AreWhereSomethingHoldsHim()
    {
        // Arrange
        var state = State("""
            P -- @
            H    @
            H@@  @
            @@@@@@
            """);

        // Act
        var places = new LevelGraph(state).Places;

        // Assert
        TilePosition[] held = [new(0, 0), new(0, 1), new(0, 2), new(1, 1), new(2, 1), new(2, 0), new(3, 0), new(3, 2)];
        TilePosition[] inTheAir = [new(1, 0), new(4, 0), new(3, 1), new(4, 1)];
        Assert.All(held, place => Assert.Contains(place, places));
        Assert.All(inTheAir, tile => Assert.DoesNotContain(tile, places));
    }

    [Fact]
    public void RunsFrom_AlongAFloor_GoesToTheWall()
    {
        // Arrange
        var graph = new LevelGraph(State("""
            @P    @
            @@@@@@@
            """));

        // Act
        var runs = graph.RunsFrom(new TilePosition(1, 0));

        // Assert
        var run = Assert.Single(runs);
        Assert.Equal(MoveKind.Right, run.Way);
        Assert.Equal(new TilePosition(5, 0), run.To);
        Assert.Equal(4, run.Moves.Count);
        Assert.Equal(Enumerable.Repeat(Joystick.Right, 20), run.Passes);
    }

    [Fact]
    public void RunsFrom_AlongABrickFloor_GoesPastWhereHeCouldDig()
    {
        // Arrange
        var graph = new LevelGraph(State("""
            @P    @
            @######
            @@@@@@@
            """));

        // Act
        var run = Assert.Single(graph.RunsFrom(new TilePosition(1, 0)));

        // Assert
        Assert.Equal(new TilePosition(5, 0), run.To);
    }

    [Fact]
    public void RunsFrom_TowardsALadderDown_StopsAboveIt()
    {
        // Arrange
        var graph = new LevelGraph(State("""
            @P    @
            @@@H@@@
            @  H  @
            @@@@@@@
            """));

        // Act
        var run = Assert.Single(graph.RunsFrom(new TilePosition(1, 0)));

        // Assert
        Assert.Equal(new TilePosition(3, 0), run.To);
        Assert.Equal(2, run.Moves.Count);
    }

    [Fact]
    public void RunsFrom_OffAnEdge_EndsWhereHeLands()
    {
        // Arrange
        var graph = new LevelGraph(State("""
            @P
            @@
            @
            @@@@
            """));

        // Act
        var run = Assert.Single(graph.RunsFrom(new TilePosition(1, 0)));

        // Assert
        Assert.Equal(new TilePosition(2, 2), run.To);
        Assert.Single(run.Moves);
        Assert.Equal(13, run.Passes.Count);
    }

    [Fact]
    public void RunsFrom_TowardsGold_StopsAtIt()
    {
        // Arrange
        var graph = new LevelGraph(State("""
            @P $  @
            @@@@@@@
            """));

        // Act
        var run = Assert.Single(graph.RunsFrom(new TilePosition(1, 0)));

        // Assert
        Assert.Equal(new TilePosition(3, 0), run.To);
    }

    [Fact]
    public void RunsFrom_AlongARope_StopsAtEachTileWhereHeCanLetGo()
    {
        // Arrange
        var graph = new LevelGraph(State("""
            @P---@
            @    @
            @@@@@@
            """, on: Tile.Rope));

        // Act
        var runs = graph.RunsFrom(new TilePosition(1, 0));

        // Assert
        Assert.Equal([MoveKind.Right, MoveKind.Down], runs.Select(run => run.Way).Order());
        Assert.Equal(new TilePosition(2, 0), runs.Single(run => run.Way == MoveKind.Right).To);
    }

    [Fact]
    public void RunsFrom_NotAPlace_GivesNone()
    {
        // Arrange
        var graph = new LevelGraph(State("""
            P
            @
            """));

        // Act
        var runs = graph.RunsFrom(new TilePosition(1, 0));

        // Assert
        Assert.Empty(runs);
    }

    // Within reach

    [Fact]
    public void WithinReach_AfterAFall_DoesntGoBackUp()
    {
        // Arrange
        var state = State("""
            @
            @@
            @ P
            @@@@
            """);

        // Act
        var reach = new LevelGraph(state).WithinReach(state);

        // Assert
        Assert.Contains(new TilePosition(1, 2), reach);
        Assert.DoesNotContain(new TilePosition(1, 0), reach);
    }

    [Fact]
    public void WithinReach_InARoomOfBricks_DigsItsFloorButNotOut()
    {
        // Arrange
        var state = State("""
            #####
            #P  #
            #####
            @@@@@
            """);

        // Act
        var reach = new LevelGraph(state).WithinReach(state);

        // Assert
        TilePosition[] inside = [new(1, 1), new(2, 1), new(3, 1), new(1, 2), new(2, 2), new(3, 2)];
        Assert.Equal(inside.OrderBy(Key), reach.OrderBy(Key));
    }

    [Fact]
    public void WithinReach_BrickUnderHimBetweenWalls_CantBeDug()
    {
        // Arrange
        var state = State("""
            @P@
            @#@
            @$@
            @@@
            """);

        // Act
        var reach = new LevelGraph(state).WithinReach(state);

        // Assert
        Assert.Equal([new TilePosition(1, 0)], reach);
    }

    [Fact]
    public void WithinReach_BrickWithALadderOnIt_CantBeDug()
    {
        // Arrange
        var state = State("""
            P H@
            @##@
            @@$@
            @@@@
            """);

        // Act
        var reach = new LevelGraph(state).WithinReach(state);

        // Assert
        Assert.Contains(new TilePosition(1, 1), reach);
        Assert.DoesNotContain(new TilePosition(2, 1), reach);
        Assert.DoesNotContain(new TilePosition(2, 2), reach);
    }

    [Fact]
    public void WithinReach_BrickWithGoldOnIt_CanBeDugOnceTheGoldIsTaken()
    {
        // Arrange
        var state = State("""
            P$@
            @#@
            @$@
            @@@
            """);

        // Act
        var reach = new LevelGraph(state).WithinReach(state);

        // Assert
        Assert.Contains(new TilePosition(1, 2), reach);
    }

    [Fact]
    public void WithinReach_GoldThreeBricksDown_IsWithinReach()
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
        var reach = new LevelGraph(state).WithinReach(state);

        // Assert
        Assert.Contains(new TilePosition(4, 4), reach);
    }

    [Fact]
    public void WithinReach_GoldUnderABrickUnderASolidBlock_IsNotWithinReach()
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
        var reach = new LevelGraph(state).WithinReach(state);

        // Assert
        Assert.DoesNotContain(new TilePosition(4, 4), reach);
    }

    [Fact]
    public void WithinReach_BrickBelowAHoleDugLater_CanBeDugFromWhereHeWasBefore()
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
        var reach = new LevelGraph(state).WithinReach(state);

        // Assert
        Assert.Contains(new TilePosition(2, 3), reach);
    }

    [Fact]
    public void WithinReach_WallHeCouldOnlyDigFromAbove_StaysShut()
    {
        // Arrange
        var state = State("""
            @@@@@
            @@ H@
            @P#H@
            @@@@@
            """);

        // Act
        var reach = new LevelGraph(state).WithinReach(state);

        // Assert
        Assert.Equal([new TilePosition(1, 2)], reach);
    }

    [Fact]
    public void WithinReach_HoleBesideHimInAWall_LetsHimThrough()
    {
        // Arrange
        var state = State("""
            @   @
            @Po @
            @@@@@
            """);

        // Act
        var reach = new LevelGraph(state).WithinReach(state);

        // Assert
        Assert.Contains(new TilePosition(3, 1), reach);
    }

    [Fact]
    public void WithinReach_OutOfAHoleIntoTheAir_FallsUntilHeLands()
    {
        // Arrange
        var state = State("""
            @  @
            @P @
            @# @
            @@ @
            @@@@
            """, on: Tile.Brick, standsInHole: true);

        // Act
        var reach = new LevelGraph(state).WithinReach(state);

        // Assert
        Assert.Contains(new TilePosition(2, 3), reach);
        Assert.DoesNotContain(new TilePosition(1, 0), reach);
    }

    [Fact]
    public void WithinReach_HoleOpenBelowGold_LetsHimDownThroughIt()
    {
        // Arrange
        var state = State("""
            P$@
            @o@
            @ @
            @@@
            """);

        // Act
        var reach = new LevelGraph(state).WithinReach(state);

        // Assert
        Assert.Contains(new TilePosition(1, 2), reach);
    }

    [Fact]
    public void WithinReach_HoleBesideHim_LetsHimOutOfHisOwn()
    {
        // Arrange
        var state = State("""
            @  @
            @Po@
            @@ @
            @@@@
            """, on: Tile.Brick, standsInHole: true);

        // Act
        var reach = new LevelGraph(state).WithinReach(state);

        // Assert
        Assert.Contains(new TilePosition(2, 1), reach);
        Assert.Contains(new TilePosition(2, 2), reach);
        Assert.DoesNotContain(new TilePosition(1, 0), reach);
    }

    private static (int, int) Key(TilePosition tile) => (tile.Row, tile.Column);
}
