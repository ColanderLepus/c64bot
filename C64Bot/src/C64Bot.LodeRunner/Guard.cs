namespace C64Bot.LodeRunner;

/// <summary>
/// A guard as the game has him after his last finished move: his tile, where he is within it, what he's doing, and
/// whether he carries gold, is stuck in a hole or waits to respawn.
/// </summary>
/// <remarks>
/// Read from the guard tables (D14). The guard the game is moving at the moment can be one move behind there.
/// </remarks>
/// <param name="Number">The game's number for him, 1–5.</param>
/// <param name="Position">His tile; while he waits to respawn, the tile he'll appear on.</param>
/// <param name="OffsetX">Where he is within the tile, left to right: −2 to +2, 0 when he's centred on it.</param>
/// <param name="OffsetY">Where he is within the tile, top to bottom: −2 to +2, 0 when he's centred on it.</param>
/// <param name="Pose">The picture shown for him. Guards don't dig, so never <see cref="Pose.Dig"/>.</param>
/// <param name="Facing">Which way he faces; none on a ladder.</param>
/// <param name="CarriesGold">Whether he carries a piece of gold, which he drops after a while or in a hole.</param>
/// <param name="InHole">Whether he's stuck in a hole, waiting to climb out.</param>
/// <param name="Respawning">Whether he died and waits to appear again at <paramref name="Position"/>.</param>
public sealed record Guard(
    int Number,
    TilePosition Position,
    int OffsetX,
    int OffsetY,
    Pose Pose,
    Direction? Facing,
    bool CarriesGold,
    bool InHole,
    bool Respawning)
{
    /// <summary>The most guards a level has.</summary>
    public const int MaxCount = 5;

    private const int LadderSprites = 2;

    // His sprites: these facing left, then the same facing right, then two on a ladder (see RamMap.GuardSprites).
    // Unlike the player, a guard has no picture for digging.
    private static readonly Pose[] PosesFacingLeft =
        [Pose.Run, Pose.Run, Pose.Run, Pose.Rope, Pose.Rope, Pose.Rope, Pose.Fall];

    /// <summary>
    /// Reads all guards from the C64's RAM, or gives null if the values aren't those of guards in a level, as
    /// before a level is set up.
    /// </summary>
    /// <param name="ram">The C64's 64 KB of RAM, for example <c>LibretroCore.SystemRam</c>.</param>
    public static IReadOnlyList<Guard>? ReadAll(ReadOnlySpan<byte> ram)
    {
        int count = ram[RamMap.GuardCount];
        if (count > MaxCount)
            return null;

        var guards = new Guard[count];
        for (var n = 1; n <= count; n++)
        {
            var guard = Read(ram, n);
            if (guard is null)
                return null;

            guards[n - 1] = guard;
        }

        return guards;
    }

    private static Guard? Read(ReadOnlySpan<byte> ram, int n)
    {
        if (Figure.ReadSprite(ram[RamMap.GuardSprites + n], PosesFacingLeft, LadderSprites) is not { } sprite
            || !Figure.TryReadPlace(
                ram[RamMap.GuardColumns + n], ram[RamMap.GuardRows + n],
                ram[RamMap.GuardStepsX + n], ram[RamMap.GuardStepsY + n],
                out var position, out var offsetX, out var offsetY))
        {
            return null;
        }

        // The timer is negative while he carries gold and positive while he's in a hole; while he waits to respawn
        // it's positive too, so the respawn timer tells that apart.
        var timer = (sbyte)ram[RamMap.GuardTimers + n];
        var respawning = ram[RamMap.GuardRespawnTimers + n] != 0;

        return new Guard(
            n,
            position,
            offsetX,
            offsetY,
            sprite.Pose,
            sprite.Facing,
            CarriesGold: timer < 0,
            InHole: timer > 0 && !respawning,
            Respawning: respawning);
    }
}
