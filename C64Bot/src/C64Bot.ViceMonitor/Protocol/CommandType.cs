namespace C64Bot.ViceMonitor.Protocol;

/// <summary>
/// The kinds of command that can be sent to VICE's binary monitor.
/// </summary>
/// <remarks>
/// Sent as byte 10 of the command header. Only the commands this project uses are listed. Section
/// 13.4 of the VICE manual describes all of them, including the layout of each command's body.
/// </remarks>
public enum CommandType : byte
{
    MemoryGet = 0x01,
    MemorySet = 0x02,

    /// <summary>
    /// Sets a checkpoint: an address range that triggers when the C64's CPU executes, reads or writes it.
    /// It can stop the emulator when hit, like a breakpoint in a debugger.
    /// </summary>
    CheckpointSet = 0x12,

    CheckpointDelete = 0x13,

    /// <summary>
    /// Saves a snapshot of the whole emulated machine to a file.
    /// </summary>
    Dump = 0x41,

    /// <summary>
    /// Loads a snapshot saved with <see cref="Dump"/>, putting the machine back in that exact state.
    /// </summary>
    Undump = 0x42,

    /// <summary>
    /// Puts text into the C64's keyboard buffer, as if typed. Only programs that read keys through that
    /// buffer see it; games that scan the keyboard themselves may not.
    /// </summary>
    KeyboardFeed = 0x72,

    /// <summary>
    /// Does nothing but get an empty answer back. Useful to check that the connection works.
    /// </summary>
    Ping = 0x81,

    DisplayGet = 0x84,
    PaletteGet = 0x91,

    /// <summary>
    /// Sets the state of a joystick port: which directions and the fire button are pressed.
    /// </summary>
    JoyportSet = 0xA2,

    /// <summary>
    /// Resumes the emulator after a command stopped it. Doesn't close VICE or the connection.
    /// </summary>
    Exit = 0xAA,
}
