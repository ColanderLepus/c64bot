namespace C64Bot.LodeRunner;

/// <summary>
/// The player as the game has him: his tile, where he is within it, and what he's doing.
/// </summary>
/// <param name="Position">The tile he's on.</param>
/// <param name="OffsetX">
/// Where he is within the tile, left to right: −2 to +2, 0 when he's centred on it. The game moves him in steps of
/// these.
/// </param>
/// <param name="OffsetY">Where he is within the tile, top to bottom: −2 to +2, 0 when he's centred on it.</param>
/// <param name="Pose">The picture shown for him.</param>
/// <param name="Facing">Which way he faces; none on a ladder, whose pictures face neither way.</param>
/// <param name="Falling">Whether he's falling.</param>
/// <param name="Digging">Which side he's digging, if he is.</param>
public sealed record Player(
    TilePosition Position,
    int OffsetX,
    int OffsetY,
    Pose Pose,
    Direction? Facing,
    bool Falling,
    Direction? Digging)
{
    private const int LadderSprites = 2;

    // His sprites: these facing left, then the same facing right, then two on a ladder (see RamMap.PlayerSprite).
    private static readonly Pose[] PosesFacingLeft =
        [Pose.Run, Pose.Run, Pose.Run, Pose.Rope, Pose.Rope, Pose.Rope, Pose.Dig, Pose.Fall];

    /// <summary>
    /// Reads the player from the C64's RAM, or gives null if the values aren't those of a player in a level, as
    /// before a level is set up.
    /// </summary>
    /// <param name="ram">The C64's 64 KB of RAM, for example <c>LibretroCore.SystemRam</c>.</param>
    public static Player? Read(ReadOnlySpan<byte> ram)
    {
        if (Figure.ReadSprite(ram[RamMap.PlayerSprite], PosesFacingLeft, LadderSprites) is not { } sprite
            || !Figure.TryReadPlace(
                ram[RamMap.PlayerColumn], ram[RamMap.PlayerRow], ram[RamMap.PlayerStepX], ram[RamMap.PlayerStepY],
                out var position, out var offsetX, out var offsetY))
        {
            return null;
        }

        Direction? digging = ram[RamMap.PlayerDigging] switch
        {
            0x01 => Direction.Right,
            0xFF => Direction.Left,
            _ => null,
        };

        return new Player(
            position, offsetX, offsetY, sprite.Pose, sprite.Facing, ram[RamMap.PlayerFalling] == 0, digging);
    }
}
