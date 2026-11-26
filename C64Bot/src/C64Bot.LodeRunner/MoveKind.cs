namespace C64Bot.LodeRunner;

/// <summary>
/// What the player does in a <see cref="Move"/>.
/// </summary>
public enum MoveKind
{
    /// <summary>
    /// A step to the left until he's in the middle of a tile, on the floor or along a rope, and the fall that may
    /// follow.
    /// </summary>
    Left,

    /// <summary>
    /// The same as <see cref="Left"/>, to the right.
    /// </summary>
    Right,

    /// <summary>
    /// A climb up a ladder until he's in the middle of a tile, which can be the one above its top.
    /// </summary>
    Up,

    /// <summary>
    /// A climb down a ladder until he's in the middle of a tile, or letting go of a rope, and the fall that may follow.
    /// </summary>
    Down,

    /// <summary>
    /// A dig below and beside him on the left, until the hole is open. If his last move left or right was to the
    /// right, a step to the left turns him first.
    /// </summary>
    DigLeft,

    /// <summary>
    /// The same as <see cref="DigLeft"/>, to the right.
    /// </summary>
    DigRight,

    /// <summary>
    /// Standing still until the next hole closes.
    /// </summary>
    WaitForHole,

    /// <summary>
    /// A fall until he lands, when nothing holds him. Then it's his only move, since the joystick does nothing.
    /// </summary>
    Fall,
}
