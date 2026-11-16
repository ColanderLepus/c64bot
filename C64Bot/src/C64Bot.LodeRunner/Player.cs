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
    private const int CentreStep = 2;
    private const int MaxStep = 4;
    private const int FirstLadderSprite = 0x10;
    private const int LastLadderSprite = 0x11;
    private const int FirstRightSprite = 0x08;

    /// <summary>
    /// Reads the player from the C64's RAM, or gives null if the values aren't those of a player in a level, as
    /// before a level is set up.
    /// </summary>
    /// <param name="ram">The C64's 64 KB of RAM, for example <c>LibretroCore.SystemRam</c>.</param>
    public static Player? Read(ReadOnlySpan<byte> ram)
    {
        int column = ram[RamMap.PlayerColumn], row = ram[RamMap.PlayerRow];
        int stepX = ram[RamMap.PlayerStepX], stepY = ram[RamMap.PlayerStepY];
        int sprite = ram[RamMap.PlayerSprite];
        if (column >= Level.Width || row >= Level.Height || stepX > MaxStep || stepY > MaxStep || sprite > LastLadderSprite)
            return null;

        var (pose, facing) = sprite >= FirstLadderSprite
            ? (Pose.Ladder, (Direction?)null)
            : ((sprite % FirstRightSprite) switch
            {
                <= 2 => Pose.Run,
                <= 5 => Pose.Rope,
                6 => Pose.Dig,
                _ => Pose.Fall,
            }, sprite < FirstRightSprite ? Direction.Left : Direction.Right);

        Direction? digging = ram[RamMap.PlayerDigging] switch
        {
            0x01 => Direction.Right,
            0xFF => Direction.Left,
            _ => null,
        };

        return new Player(
            new TilePosition(column, row),
            stepX - CentreStep,
            stepY - CentreStep,
            pose,
            facing,
            ram[RamMap.PlayerFalling] == 0,
            digging);
    }
}
