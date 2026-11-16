namespace C64Bot.LodeRunner;

/// <summary>
/// A hole the player has dug: a brick that's gone for a while, and how long until it's back.
/// </summary>
/// <remarks>
/// A hole exists from the moment the dig finishes. Anyone in it when it closes dies: a guard respawns, the player
/// loses a life. The game shows it filling in from <see cref="ClosingPasses"/> passes before it closes, but it stays
/// open until then.
/// </remarks>
/// <param name="Position">The tile that was dug.</param>
/// <param name="PassesLeft">
/// The passes of the game's main loop until the hole closes, 1 to <see cref="OpenPasses"/>; about 30 passes a second.
/// </param>
public sealed record Hole(TilePosition Position, int PassesLeft)
{
    /// <summary>The most holes there can be at once.</summary>
    public const int MaxCount = 30;

    /// <summary>How many passes a new hole stays open: 180, about 6 seconds.</summary>
    public const int OpenPasses = 180;

    /// <summary>From how many passes left the game shows the hole filling in: 20.</summary>
    public const int ClosingPasses = 20;

    /// <summary>
    /// Reads the holes from the C64's RAM, or gives null if a hole isn't in the level, as when the values are
    /// leftovers.
    /// </summary>
    /// <param name="ram">The C64's 64 KB of RAM, for example <c>LibretroCore.SystemRam</c>.</param>
    public static IReadOnlyList<Hole>? ReadAll(ReadOnlySpan<byte> ram)
    {
        var holes = new List<Hole>();
        for (var slot = 0; slot < MaxCount; slot++)
        {
            int passesLeft = ram[RamMap.HoleTimers + slot];
            if (passesLeft == 0)
                continue;

            int column = ram[RamMap.HoleColumns + slot], row = ram[RamMap.HoleRows + slot];
            if (column >= Level.Width || row >= Level.Height || passesLeft > OpenPasses)
                return null;

            holes.Add(new Hole(new TilePosition(column, row), passesLeft));
        }

        return holes;
    }
}
