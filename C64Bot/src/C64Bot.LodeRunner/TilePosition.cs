namespace C64Bot.LodeRunner;

/// <summary>
/// A tile's place in the level: column 0–27 from the left, row 0–15 from the top.
/// </summary>
public readonly record struct TilePosition(int Column, int Row);
