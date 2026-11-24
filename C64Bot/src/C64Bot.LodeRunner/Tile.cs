namespace C64Bot.LodeRunner;

/// <summary>
/// What a tile of the level is, with the game's own codes for it.
/// </summary>
/// <remarks>
/// A level from the running game has no hidden ladders, guards or player among its tiles (see <see cref="Level"/>). A
/// value that isn't one of these is kept as it is, so it can be spotted.
/// </remarks>
public enum Tile : byte
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
    /// An exit ladder that only appears once all gold is taken.
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
