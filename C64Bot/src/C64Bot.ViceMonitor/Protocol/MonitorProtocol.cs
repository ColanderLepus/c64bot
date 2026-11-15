namespace C64Bot.ViceMonitor.Protocol;

/// <summary>
/// Constants shared by commands and responses in VICE's binary monitor protocol.
/// </summary>
/// <remarks>
/// The binary monitor is a network interface VICE offers so another program can control the
/// emulator: read and write memory, stop and resume it, send input. VICE turns it on when started
/// with <c>-binarymonitor</c> and listens on TCP port 6502. See chapter 13 of the VICE manual.
/// </remarks>
public static class MonitorProtocol
{
    /// <summary>
    /// The first byte of every command and response, marking where a message starts.
    /// </summary>
    /// <remarks>
    /// STX is "start of text" in ASCII.
    /// </remarks>
    public const byte Stx = 0x02;

    /// <summary>
    /// The protocol version this client speaks. VICE 3.10 uses version 2.
    /// </summary>
    public const byte ApiVersion = 0x02;
}
