namespace C64Bot.Vice.Protocol;

/// <summary>
/// Command types for the VICE binary monitor protocol.
/// </summary>
/// <remarks>
/// See chapter 13 of the VICE manual for details on each command type.
/// </remarks>
public enum CommandType : byte
{
    MemoryGet = 0x01,
    MemorySet = 0x02,
    CheckpointSet = 0x12,
    Dump = 0x41,
    Undump = 0x42,
    KeyboardFeed = 0x72,
    Ping = 0x81,
    DisplayGet = 0x84,
    PaletteGet = 0x91,
    JoyportSet = 0xA2,
    Exit = 0xAA,
}
