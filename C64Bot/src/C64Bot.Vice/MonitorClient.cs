using System.Collections.Concurrent;
using System.Net.Sockets;
using C64Bot.Vice.Protocol;
using System.Threading.Channels;


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

    /// <summary>
    /// The events VICE has sent on its own, such as "stopped" and "resumed", in the order they arrived.
    /// </summary>
    /// <remarks>
    /// Holds at most the newest 100 events; older ones are dropped if nobody reads them. Completes when the
    /// connection closes.
    /// </remarks>
    public ChannelReader<MonitorResponse> Events => _events.Reader;

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
    /// Sends a command and waits for VICE's answer to it.
    /// </summary>
    /// <remarks>
    /// A command that fails is still answered, so check the answer's <see cref="MonitorResponse.Error"/>.
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
    /// Sends a ping, which does nothing but get an answer back. Useful to check that the connection works.
    /// </summary>
    public Task<MonitorResponse> PingAsync(CancellationToken cancellationToken = default) =>
        SendAsync(CommandType.Ping, cancellationToken: cancellationToken);

    /// <summary>
    /// Resumes the emulator after a command stopped it. Doesn't close the connection.
    /// </summary>
    public Task<MonitorResponse> ExitAsync(CancellationToken cancellationToken = default) =>
        SendAsync(CommandType.Exit, cancellationToken: cancellationToken);
    
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

    public async ValueTask DisposeAsync()
    {
        _shutdown.Cancel();
        _tcpClient.Dispose();
        await _readLoop;
        _shutdown.Dispose();
        _writeLock.Dispose();
    }


}
