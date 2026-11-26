using static C64Bot.LodeRunner.Tests.TestStates;

namespace C64Bot.LodeRunner.Tests;

public class PitTests
{
    // Level 10 without guards, with only the gold in the box at L4 and L5 left: four rows of bricks above the box, and
    // the ladder in column 10 beside it.
    private const string Level10 = """

                H#######
                H      ######
        ##########H
                  H###### #####H##
                P H            H
        ##########H            H
        ##########H#######@@   H####
        ##########H            H
        ##########H         ---H
        ##      ##H       ##   H
        ##  $$  ##H            H
        ###########H#######H   H
                   H       ########H
                   H               H
        ############################
        """;

    [Fact]
    public void AllIn_BesideALadderThreeColumnsAway_StopsAtTheLadder()
    {
        // Arrange
        var state = State(Level10);

        // Act
        var pit = Single(state, new TilePosition(7, 6), Direction.Right);

        // Assert: G7 from F8, G8 from F9, G9 from the ladder at F10, then a layer down, and so on.
        Assert.Equal(
            [
                Dig(7, 6, 8, 5), Dig(8, 6, 9, 5), Dig(9, 6, 10, 5),
                Dig(7, 7, 8, 6), Dig(8, 7, 9, 6), Dig(9, 7, 10, 6),
                Dig(7, 8, 8, 7), Dig(8, 8, 9, 7),
                Dig(7, 9, 8, 8),
            ],
            pit.Digs);
        Assert.Equal(new TilePosition(7, 11), pit.LandsAt);
        Assert.Equal([new TilePosition(7, 10)], pit.FallsPast);
    }

    [Fact]
    public void AllIn_WithNothingToStandOnBeside_IsATriangle()
    {
        // Arrange
        var state = State(Level10);

        // Act
        var pit = Single(state, new TilePosition(7, 6), Direction.Left);

        // Assert
        Assert.Equal([4, 3, 2, 1], pit.Digs.GroupBy(dig => dig.Hole.Row).Select(layer => layer.Count()));
        Assert.Equal(
            [new TilePosition(7, 6), new TilePosition(6, 6), new TilePosition(5, 6), new TilePosition(4, 6)],
            pit.Digs.Take(4).Select(dig => dig.Hole));
    }

    [Fact]
    public void AllIn_RightBesideALadder_IsTheColumnAlone()
    {
        // Arrange
        var state = State("""
             P
            @H#@
            @H#@
            @H#@
            @  @
            @@@@
            """);

        // Act
        var pit = Single(state, new TilePosition(2, 1), Direction.Left);

        // Assert
        Assert.Equal([Dig(2, 1, 1, 0), Dig(2, 2, 1, 1), Dig(2, 3, 1, 2)], pit.Digs);
        Assert.Equal(new TilePosition(2, 4), pit.LandsAt);
    }

    [Fact]
    public void AllIn_SolidBlockWhereItWouldGo_GoesOutTheOtherWay()
    {
        // Arrange
        var state = State("""
               P
            ########
            ####@###
            ########
            @@@ @@@@
            @@@@@@@@
            """);

        // Act
        var pits = Pit.AllIn(state, new LevelGraph(state)).Where(pit => pit.Top == new TilePosition(3, 1)).ToList();

        // Assert
        var pit = Assert.Single(pits);
        Assert.Equal(Direction.Left, pit.Side);
        Assert.Equal(6, pit.Digs.Count);
    }

    private static Pit Single(GameState state, TilePosition top, Direction side) =>
        Assert.Single(Pit.AllIn(state, new LevelGraph(state)), pit => pit.Top == top && pit.Side == side);

    private static PitDig Dig(int column, int row, int fromColumn, int fromRow) =>
        new(new TilePosition(column, row), new TilePosition(fromColumn, fromRow));
}
