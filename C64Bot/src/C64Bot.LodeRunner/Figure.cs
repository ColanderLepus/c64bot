namespace C64Bot.LodeRunner;

// What the player and the guards have in common: the game keeps each figure's tile, its place within the tile in
// steps of 0-4 (2 when centred), and a sprite number that says its pose and facing.
internal static class Figure
{
    private const int CentreStep = 2;
    private const int MaxStep = 4;

    // The tile and the offsets (-2 to +2, 0 when centred) from the game's column, row and steps. False if they aren't
    // those of a figure in a level, as before a level is set up.
    public static bool TryReadPlace(
        int column, int row, int stepX, int stepY, out TilePosition position, out int offsetX, out int offsetY)
    {
        position = new TilePosition(column, row);
        offsetX = stepX - CentreStep;
        offsetY = stepY - CentreStep;
        return column < Level.Width && row < Level.Height && stepX <= MaxStep && stepY <= MaxStep;
    }

    // The pose and facing from a sprite number. The sprites facing left come first, one per entry in
    // posesFacingLeft, then the same ones facing right, then the ladder sprites, which face neither way. Null for a
    // number beyond them.
    public static (Pose Pose, Direction? Facing)? ReadSprite(int sprite, Pose[] posesFacingLeft, int ladderSprites)
    {
        var perFacing = posesFacingLeft.Length;
        if (sprite >= 2 * perFacing + ladderSprites)
            return null;

        if (sprite >= 2 * perFacing)
            return (Pose.Ladder, null);

        return (posesFacingLeft[sprite % perFacing], sprite < perFacing ? Direction.Left : Direction.Right);
    }
}
