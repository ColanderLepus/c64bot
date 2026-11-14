using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading.Channels;
using C64Bot.Vice.Protocol;

namespace C64Bot.Vice;

/// <summary>
/// A connection to VICE's binary monitor that sends commands and waits for their answers.
/// </summary>
/// <remarks>
/// <para>
/// The binary monitor is a network interface VICE offers so another program can control the emulator.
/// VICE turns it on when started with <c>-binarymonitor</c>, and then listens on TCP port 6502.
/// </para>
/// <para>
/// Every command gets its own request ID. A background loop reads everything VICE sends and hands each
/// answer to the command with the same request ID. Events, which VICE sends on its own (for example
/// "the emulator has stopped"), go to <see cref="Events"/>.
/// </para>
/// <para>
/// Sending any command stops the emulator until an <see cref="CommandType.Exit"/> command resumes it.
/// </para>
/// </remarks>
public sealed class MonitorClient : IAsyncDisposable
{
    public const int DefaultPort = 6502;
    private const int EventBufferSize = 100;
    private const int MemoryRangeLength = 8; // Side effects (1) + start address (2) + end address (2) + memspace (1) + bank ID (2).

    private readonly TcpClient _tcpClient;
    private readonly NetworkStream _stream;
    private readonly Channel<MonitorResponse> _events = Channel.CreateBounded<MonitorResponse>(
        new BoundedChannelOptions(EventBufferSize) { FullMode = BoundedChannelFullMode.DropOldest, SingleWriter = true });

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ConcurrentDictionary<uint, TaskCompletionSource<MonitorResponse>> _pendingRequests = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _readLoop;

    private uint _lastRequestId;
    private Exception? _failure;

    private MonitorClient(TcpClient tcpClient)
    {
        _tcpClient = tcpClient;
        _stream = tcpClient.GetStream();
        _readLoop = Task.Run(ReadLoopAsync);
    }

    /// <summary>
    /// Connects to a running VICE.
    /// </summary>
    /// <exception cref="SocketException">
    /// Nothing answered, for example because VICE isn't running or wasn't started with <c>-binarymonitor</c>.
    /// </exception>
    public static async Task<MonitorClient> ConnectAsync(
        string host = "127.0.0.1", int port = DefaultPort, CancellationToken cancellationToken = default)
    {
        var tcpClient = new TcpClient();
        try
        {
            await tcpClient.ConnectAsync(host, port, cancellationToken);
        }
        catch
        {
            tcpClient.Dispose();
            throw;
        }

        return new MonitorClient(tcpClient);
    }

    /// <summary>
    /// The events VICE has sent on its own, such as "stopped" and "resumed", in the order they arrived.
    /// </summary>
    /// <remarks>
    /// Holds at most the newest 100 events; older ones are dropped if nobody reads them. Completes when the
    /// connection closes.
    /// </remarks>
    public ChannelReader<MonitorResponse> Events => _events.Reader;

    /// <summary>
    /// Sends a command and waits for VICE's answer to it.
    /// </summary>
    /// <remarks>
    /// For commands that don't have their own method yet. A command that fails is still answered, so check the
    /// answer's <see cref="MonitorResponse.Error"/>.
    /// </remarks>
    /// <exception cref="IOException">The connection to VICE was lost before the answer arrived.</exception>
    public async Task<MonitorResponse> SendAsync(
        CommandType type, ReadOnlyMemory<byte> body = default, CancellationToken cancellationToken = default)
    {
        var requestId = Interlocked.Increment(ref _lastRequestId);
        var answer = new TaskCompletionSource<MonitorResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[requestId] = answer;

        try
        {
            // If the read loop has already stopped, no answer will ever come.
            if (Volatile.Read(ref _failure) is { } failure)
                throw failure;

            var bytes = new MonitorCommand(type, requestId, body).Encode();

            await _writeLock.WaitAsync(cancellationToken);
            try
            {
                await _stream.WriteAsync(bytes, cancellationToken);
            }
            finally
            {
                _writeLock.Release();
            }

            return await answer.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            _pendingRequests.TryRemove(requestId, out _);
        }
    }

    /// <summary>
    /// Reads memory from <paramref name="start"/> to <paramref name="end"/>, both included.
    /// </summary>
    /// <param name="memspace">Whose memory to read: the C64's own, or a disk drive's.</param>
    /// <param name="bankId">
    /// Which bank to read. At some addresses the C64 can show RAM, ROM or I/O, and the bank chooses between
    /// them. The IDs differ per machine and are listed by VICE's Banks available command (section 13.4.19 of
    /// the VICE manual).
    /// </param>
    /// <param name="sideEffects">
    /// Whether the read may change the machine's state. Reading some hardware registers does that, for
    /// example by acknowledging an interrupt, so it's off unless asked for.
    /// </param>
    /// <exception cref="MonitorCommandException">VICE answered with an error.</exception>
    public async Task<byte[]> MemoryGetAsync(
        ushort start, ushort end,
        Memspace memspace = Memspace.MainMemory, ushort bankId = 0, bool sideEffects = false,
        CancellationToken cancellationToken = default)
    {
        if (end < start)
            throw new ArgumentOutOfRangeException(nameof(end), "The end address must not be before the start address.");

        var body = new byte[MemoryRangeLength];
        WriteMemoryRange(body, start, end, memspace, bankId, sideEffects);

        var response = await SendCheckedAsync(CommandType.MemoryGet, body, cancellationToken);

        // The body starts with a 2-byte length, which is 0 when all 64 KB were read. The bytes after it are the memory.
        return response.Body[2..].ToArray();
    }

    /// <summary>
    /// Writes <paramref name="data"/> to memory, starting at <paramref name="start"/>.
    /// </summary>
    /// <param name="memspace">Whose memory to write: the C64's own, or a disk drive's.</param>
    /// <param name="bankId">Which bank to write. See <see cref="MemoryGetAsync"/>.</param>
    /// <param name="sideEffects">
    /// Whether the write may change the machine's state beyond the memory itself, for example when writing
    /// to hardware registers. Off unless asked for.
    /// </param>
    /// <exception cref="MonitorCommandException">VICE answered with an error.</exception>
    public async Task MemorySetAsync(
        ushort start, ReadOnlyMemory<byte> data,
        Memspace memspace = Memspace.MainMemory, ushort bankId = 0, bool sideEffects = false,
        CancellationToken cancellationToken = default)
    {
        if (data.IsEmpty)
            throw new ArgumentException("There must be at least one byte to write.", nameof(data));

        if (start + data.Length - 1 > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(data), "The data doesn't fit below address $FFFF.");

        var end = (ushort)(start + data.Length - 1);
        var body = new byte[MemoryRangeLength + data.Length];
        WriteMemoryRange(body, start, end, memspace, bankId, sideEffects);
        data.Span.CopyTo(body.AsSpan(MemoryRangeLength));

        await SendCheckedAsync(CommandType.MemorySet, body, cancellationToken);
    }

    /// <summary>
    /// Sends a ping, which does nothing but get an answer back. Useful to check that the connection works.
    /// </summary>
    /// <exception cref="MonitorCommandException">VICE answered with an error.</exception>
    public Task<MonitorResponse> PingAsync(CancellationToken cancellationToken = default) =>
        SendCheckedAsync(CommandType.Ping, default, cancellationToken);

    /// <summary>
    /// Resumes the emulator after a command stopped it. Doesn't close the connection.
    /// </summary>
    /// <exception cref="MonitorCommandException">VICE answered with an error.</exception>
    public Task<MonitorResponse> ExitAsync(CancellationToken cancellationToken = default) =>
        SendCheckedAsync(CommandType.Exit, default, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (_shutdown.IsCancellationRequested)
            return;

        _shutdown.Cancel();
        _tcpClient.Dispose();
        await _readLoop;
        _shutdown.Dispose();
        _writeLock.Dispose();
    }

    private async Task<MonitorResponse> SendCheckedAsync(
        CommandType type, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        var response = await SendAsync(type, body, cancellationToken);
        if (response.Error != ResponseError.Ok)
            throw new MonitorCommandException(type, response.Error);

        return response;
    }

    private static void WriteMemoryRange(
        Span<byte> body, ushort start, ushort end, Memspace memspace, ushort bankId, bool sideEffects)
    {
        body[0] = sideEffects ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteUInt16LittleEndian(body[1..3], start);
        BinaryPrimitives.WriteUInt16LittleEndian(body[3..5], end);
        body[5] = (byte)memspace;
        BinaryPrimitives.WriteUInt16LittleEndian(body[6..8], bankId);
    }

    private async Task ReadLoopAsync()
    {
        try
        {
            while (true)
            {
                var response = await MonitorResponse.ReadAsync(_stream, _shutdown.Token);

                if (response.IsEvent)
                {
                    _events.Writer.TryWrite(response);
                    continue;
                }

                if (_pendingRequests.TryGetValue(response.RequestId, out var answer))
                    answer.TrySetResult(response);
            }
        }
        catch (Exception exception)
        {
            // The connection is closed or broken: fail every command still waiting for an answer.
            var failure = new IOException("The connection to VICE was lost.", exception);
            Volatile.Write(ref _failure, failure);

            foreach (var answer in _pendingRequests.Values)
                answer.TrySetException(failure);

            // Readers of Events see the end of the queue. A deliberate shutdown isn't an error.
            if (_shutdown.IsCancellationRequested)
                _events.Writer.TryComplete();
            else
                _events.Writer.TryComplete(failure);
        }
    }
}
