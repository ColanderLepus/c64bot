namespace C64Bot.LodeRunner;

/// <summary>
/// The picture the game shows for a figure: running, hanging from a rope, digging, falling or climbing.
/// </summary>
/// <remarks>
/// It's the picture, not the movement: a figure standing still keeps the picture of his last move.
/// </remarks>
public enum Pose
{
    Run,
    Rope,
    Dig,
    Fall,
    Ladder,
}
