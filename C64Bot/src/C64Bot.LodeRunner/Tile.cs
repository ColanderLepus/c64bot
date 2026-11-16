namespace C64Bot.LodeRunner;

/// <summary>
/// What a tile of the level is, with the game's own codes for it.
/// </summary>
/// <remarks>
/// The grid as built only holds these. The game's codes 6 (hidden exit ladder), 8 (guard) and 9 (player) are only in
/// the level as stored on disk, and are taken out when the level starts. A value that isn't one of these is kept as
/// it is, so it can be spotted.
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

    Gold = 7,
}
