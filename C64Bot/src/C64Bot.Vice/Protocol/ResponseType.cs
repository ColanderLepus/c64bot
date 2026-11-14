namespace C64Bot.Vice.Protocol;

/// <summary>
/// Response types for the VICE binary monitor protocol.
/// </summary>
/// <remarks>
/// A response usually has the same type as the command it answers. Events, which VICE sends
/// on its own, have their own types. See sections 13.4 and 13.5 of the VICE manual.
/// </remarks>
public enum ResponseType : byte
{
    Invalid = 0x00,
    MemoryGet = 0x01,
    MemorySet = 0x02,
    CheckpointInfo = 0x11,
    RegisterInfo = 0x31,
    Dump = 0x41,
    Undump = 0x42,
    Jam = 0x61,
    Stopped = 0x62,
    Resumed = 0x63,
    KeyboardFeed = 0x72,
    Ping = 0x81,
    DisplayGet = 0x84,
    PaletteGet = 0x91,
    JoyportSet = 0xA2,
    Exit = 0xAA,
}
