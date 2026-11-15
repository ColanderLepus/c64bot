using System.Buffers.Binary;

namespace C64Bot.ViceMonitor.Protocol;

/// <summary>
/// A message from VICE's binary monitor: either the answer to a command, or an event VICE sends on its own.
/// </summary>
/// <remarks>
/// <para>
/// Received as a 12-byte header followed by the body. The header holds the start byte
/// (<see cref="MonitorProtocol.Stx"/>), the protocol version, the length of the body, the response
/// type, an error code and a request ID. All numbers are little-endian (lowest byte first). See
/// section 13.2 of the VICE manual.
/// </para>
/// <para>
/// An answer carries the request ID of the command it answers. An event, such as "the emulator has
/// stopped", answers no command and carries <see cref="EventRequestId"/> instead. Answers and events
/// arrive mixed together on the same connection.
/// </para>
/// </remarks>
/// <param name="type">What kind of response this is. Usually the same as the type of the command it answers.</param>
/// <param name="error">Whether the command succeeded, and if not, why.</param>
/// <param name="requestId">The request ID of the command this answers, or <see cref="EventRequestId"/> for an event.</param>
/// <param name="body">The response's data, laid out as sections 13.4 and 13.5 of the VICE manual describe for this response type.</param>
public sealed class MonitorResponse(ResponseType type, ResponseError error, uint requestId, ReadOnlyMemory<byte> body)
{
    public const int HeaderLength = 12;

    /// <summary>
    /// The request ID VICE puts in events, since they don't answer any command.
    /// </summary>
    public const uint EventRequestId = 0xFFFFFFFF;

    public ResponseType Type { get; } = type;
    public ResponseError Error { get; } = error;
    public uint RequestId { get; } = requestId;
    public ReadOnlyMemory<byte> Body { get; } = body;

    /// <summary>
    /// True when VICE sent this on its own, for example to report that the emulator stopped, rather
    /// than as the answer to a command.
    /// </summary>
    public bool IsEvent => RequestId == EventRequestId;

    /// <summary>
    /// Reads exactly one response from the stream: first the header, then as many body bytes as the header says.
    /// </summary>
    /// <remarks>
    /// The protocol has no end marker, so the body length in the header is the only way to know where
    /// one response ends and the next begins. A wrong start byte or version means the reader has lost
    /// its place in the stream, so it throws instead of guessing.
    /// </remarks>
    /// <exception cref="InvalidDataException">
    /// The response doesn't start with <see cref="MonitorProtocol.Stx"/>, or uses a protocol version
    /// other than <see cref="MonitorProtocol.ApiVersion"/>.
    /// </exception>
    /// <exception cref="EndOfStreamException">The stream ended before the whole response was read.</exception>
    public static async Task<MonitorResponse> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var header = new byte[HeaderLength];
        await stream.ReadExactlyAsync(header, cancellationToken);

        if (header[0] != MonitorProtocol.Stx)
            throw new InvalidDataException($"Expected STX (0x02) at the start of a response, got 0x{header[0]:X2}.");

        if (header[1] != MonitorProtocol.ApiVersion)
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
