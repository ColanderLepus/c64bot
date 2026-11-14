using System.Buffers.Binary;

namespace C64Bot.Vice.Protocol;

/// <summary>
/// Represents a command sent to the VICE monitor.
/// </summary>
/// <param name="type">The type of the command.</param>
/// <param name="requestId">The request ID for matching responses.</param>
/// <param name="body">The body of the command.</param>
public sealed class MonitorCommand(CommandType type, uint requestId, ReadOnlyMemory<byte> body)
{
    public const byte Stx = 0x02;
    public const byte ApiVersion = 0x02;
    public const int HeaderLength = 11;

    public CommandType Type { get; } = type;
    public uint RequestId { get; } = requestId;
    public ReadOnlyMemory<byte> Body { get; } = body;

    /// <summary>
    /// Encodes the command into a byte array suitable for sending to VICE.
    /// </summary>
    /// <returns>The encoded command as a byte array.</returns>
    public byte[] Encode()
    {
        var bytes = new byte[HeaderLength + Body.Length];
        bytes[0] = Stx;
        bytes[1] = ApiVersion;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(2, 4), (uint)Body.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(6, 4), RequestId);
        bytes[10] = (byte)Type;
        Body.Span.CopyTo(bytes.AsSpan(HeaderLength));
        
        return bytes;
    }
}
