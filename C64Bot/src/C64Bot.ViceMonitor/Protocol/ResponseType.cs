namespace C64Bot.ViceMonitor.Protocol;

/// <summary>
/// The kinds of response VICE's binary monitor sends.
/// </summary>
/// <remarks>
/// Received as byte 6 of the response header. A value with the same name as a <see cref="CommandType"/>
/// is the answer to that command. The others are events VICE sends on its own, or answers shared by
/// several commands. Sections 13.4 and 13.5 of the VICE manual describe each one, including the layout
/// of its body.
/// </remarks>
public enum ResponseType : byte
{
    /// <summary>
    /// The command failed. The response's error code says why.
    /// </summary>
    Invalid = 0x00,

    MemoryGet = 0x01,
    MemorySet = 0x02,

    /// <summary>
    /// Details of a checkpoint. Sent as the answer to checkpoint commands, and as an event when a
    /// checkpoint is hit.
    /// </summary>
    CheckpointInfo = 0x11,

    /// <summary>
    /// The values of the C64 CPU's registers. Sent as an event whenever the emulator stops.
    /// </summary>
    RegisterInfo = 0x31,

    Dump = 0x41,
    Undump = 0x42,

    /// <summary>
    /// Event: the C64's CPU has jammed (hit an instruction that halts it), so the program has crashed.
    /// </summary>
    Jam = 0x61,

    /// <summary>
    /// Event: the emulator has stopped, for example because a command was received or a checkpoint was
    /// hit. The body holds the program counter: the address of the next instruction the CPU will run.
    /// </summary>
    Stopped = 0x62,

    /// <summary>
    /// Event: the emulator is running again. The body holds the program counter.
    /// </summary>
    Resumed = 0x63,

    KeyboardFeed = 0x72,
    Ping = 0x81,
    DisplayGet = 0x84,
    PaletteGet = 0x91,
    JoyportSet = 0xA2,
    Exit = 0xAA,
}
