namespace C64Bot.LodeRunner;

/// <summary>
/// What a tile of a level is as stored, with the game's own codes for it: what <see cref="Tile"/> has, and what the
/// game takes out of the level when it starts (hidden exit ladders, the guards and the player).
/// </summary>
/// <remarks>
/// A stored tile has room for codes up to 15. One that isn't listed here is kept as it is.
/// </remarks>
public enum LevelTile : byte
{
    Empty = 0,

    /// <summary>
    /// A brick that can be dug.
    /// </summary>
    Brick = 1,

    /// <summary>
    /// A block that can't be dug.
    /// </summary>
    Solid = 2,

    Ladder = 3,

    /// <summary>
    /// A bar to hang from and move along.
    /// </summary>
    Rope = 4,

    /// <summary>
    /// Looks like a brick, but anyone stepping on it falls through.
    /// </summary>
    TrapBrick = 5,

    /// <summary>
    /// A ladder that only appears once all gold is taken.
    /// </summary>
    HiddenLadder = 6,

    Gold = 7,

    /// <summary>
    /// Where a guard starts.
    /// </summary>
    Guard = 8,

    /// <summary>
    /// Where the player starts.
    /// </summary>
    Player = 9,
}
