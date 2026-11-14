using System.Buffers.Binary;

namespace C64Bot.Vice.Protocol;

/// <summary>
/// Represents a binary monitor. A 12-byte header followed by the body.
/// </summary>
/// <remarks>
/// See sections 13.2 of the VICE manual for details on response structure.
/// </remarks>
public sealed class MonitorResponse(ResponseType type, ResponseError error, uint requestId, ReadOnlyMemory<byte> body)
{
    public const int HeaderLength = 12;
    public const uint EventRequestId = 0xFFFFFFFF;

    public ResponseType Type { get; } = type;
    public ResponseError Error { get; } = error;
    public uint RequestId { get; } = requestId;
    public ReadOnlyMemory<byte> Body { get; } = body;

    /// <summary>
    /// True when VICE sent this on its own (for example "stopped"), not as an answer to a command.
    /// </summary>
    public bool IsEvent => RequestId == EventRequestId;

    /// <summary>
    /// Reads exactly one response from the stream: the header, then as many body bytes as the header says.
    /// </summary>
    public static async Task<MonitorResponse> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var header = new byte[HeaderLength];
        await stream.ReadExactlyAsync(header, cancellationToken);

        if(header[0] != MonitorCommand.Stx)
            throw new InvalidDataException($"Expected STX (0x02) at the start of a response, got 0x{header[0]:X2}.");
        
        if(header[1] != MonitorCommand.ApiVersion)
            throw new InvalidDataException($"Unsupported API version 0x{header[1]:X2}.");

        var bodyLength = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(2, 4));
        var type = (ResponseType)header[6];
        var error = (ResponseError)header[7];
        var requestId = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8, 4));

        var body = new byte[bodyLength];
        await stream.ReadExactlyAsync(body, cancellationToken);

        return new MonitorResponse(type, error, requestId, body);
    }
}