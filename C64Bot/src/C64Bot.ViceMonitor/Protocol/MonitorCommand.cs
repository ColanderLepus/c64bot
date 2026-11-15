using System.Buffers.Binary;

namespace C64Bot.ViceMonitor.Protocol;

/// <summary>
/// A command for VICE's binary monitor, such as "read this memory" or "resume the emulator".
/// </summary>
/// <remarks>
/// <para>
/// Sent as an 11-byte header followed by the body. The header holds the start byte
/// (<see cref="MonitorProtocol.Stx"/>), the protocol version, the length of the body, the request
/// ID and the command type. All numbers are little-endian (lowest byte first). See section 13.1 of
/// the VICE manual.
/// </para>
/// <para>
/// Sending any command stops the emulator until an <see cref="CommandType.Exit"/> command resumes it.
/// </para>
/// </remarks>
/// <param name="type">What VICE should do.</param>
/// <param name="requestId">
/// Any number the sender chooses. VICE copies it into its response, so the response can be matched to
/// this command.
/// </param>
/// <param name="body">
/// The command's parameters, laid out as section 13.4 of the VICE manual describes for this command
/// type. Empty for commands without parameters, such as <see cref="CommandType.Ping"/>.
/// </param>
public sealed class MonitorCommand(CommandType type, uint requestId, ReadOnlyMemory<byte> body)
{
    public const int HeaderLength = 11;

    public CommandType Type { get; } = type;
    public uint RequestId { get; } = requestId;
    public ReadOnlyMemory<byte> Body { get; } = body;

    /// <summary>
    /// Turns the command into the exact bytes to send to VICE: the header followed by the body.
    /// </summary>
    public byte[] Encode()
    {
        var bytes = new byte[HeaderLength + Body.Length];
        bytes[0] = MonitorProtocol.Stx;
        bytes[1] = MonitorProtocol.ApiVersion;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(2, 4), (uint)Body.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(6, 4), RequestId);
        bytes[10] = (byte)Type;
        Body.Span.CopyTo(bytes.AsSpan(HeaderLength));

        return bytes;
    }
}
