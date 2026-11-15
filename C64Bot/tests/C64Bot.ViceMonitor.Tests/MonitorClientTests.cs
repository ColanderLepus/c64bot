using System.Net;
using System.Net.Sockets;
using C64Bot.ViceMonitor.Protocol;

namespace C64Bot.ViceMonitor.Tests;

public class MonitorClientTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    // A stopped event with PC = $E5CF, as in section 13.3 of the VICE manual.
    private static readonly byte[] StoppedEvent =
        [0x02, 0x02, 0x02, 0x00, 0x00, 0x00, 0x62, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xCF, 0xE5];

    [Fact]
    public async Task MemoryGetAsync_FourBytes_SendsTheRangeAndReturnsTheMemory()
    {
        // Arrange
        byte[]? sentBody = null;
        var (port, fakeVice) = StartFakeVice(async stream =>
        {
            var (requestId, body) = await ReadCommandAsync(stream);
            sentBody = body;
            byte[] answerBody = [0x04, 0x00, 0x12, 0x05, 0x01, 0x04]; // length 4, then the memory
            await stream.WriteAsync(Answer(ResponseType.MemoryGet, ResponseError.Ok, requestId, answerBody));
        });
        await using var client = await MonitorClient.ConnectAsync("127.0.0.1", port);

        // Act
        var memory = await client.MemoryGetAsync(0x0400, 0x0403).WaitAsync(Timeout);

        // Assert
        await fakeVice;
        byte[] expectedBody =
        [
            0x00,       // no side effects
            0x00, 0x04, // start: $0400
            0x03, 0x04, // end: $0403
            0x00,       // memspace: main memory
            0x00, 0x00, // bank 0
        ];
        Assert.Equal(expectedBody, sentBody);
        Assert.Equal([0x12, 0x05, 0x01, 0x04], memory);
    }

    [Fact]
    public async Task MemoryGetAsync_ViceAnswersWithAnError_ThrowsMonitorCommandException()
    {
        // Arrange
        var (port, fakeVice) = StartFakeVice(async stream =>
        {
            var (requestId, _) = await ReadCommandAsync(stream);
            await stream.WriteAsync(Answer(ResponseType.MemoryGet, ResponseError.InvalidMemspace, requestId, []));
        });
        await using var client = await MonitorClient.ConnectAsync("127.0.0.1", port);

        // Act
        var exception = await Record.ExceptionAsync(() => client.MemoryGetAsync(0x0400, 0x0403).WaitAsync(Timeout));

        // Assert
        await fakeVice;
        var commandException = Assert.IsType<MonitorCommandException>(exception);
        Assert.Equal(ResponseError.InvalidMemspace, commandException.Error);
    }

    [Fact]
    public async Task MemorySetAsync_OneByte_SendsTheRangeAndTheData()
    {
        // Arrange
        byte[]? sentBody = null;
        var (port, fakeVice) = StartFakeVice(async stream =>
        {
            var (requestId, body) = await ReadCommandAsync(stream);
            sentBody = body;
            await stream.WriteAsync(Answer(ResponseType.MemorySet, ResponseError.Ok, requestId, []));
        });
        await using var client = await MonitorClient.ConnectAsync("127.0.0.1", port);

        // Act
        await client.MemorySetAsync(0x0400, new byte[] { 0x01 }).WaitAsync(Timeout);

        // Assert
        await fakeVice;
        byte[] expectedBody =
        [
            0x00,       // no side effects
            0x00, 0x04, // start: $0400
            0x00, 0x04, // end: $0400
            0x00,       // memspace: main memory
            0x00, 0x00, // bank 0
            0x01,       // the data
        ];
        Assert.Equal(expectedBody, sentBody);
    }

    [Fact]
    public async Task PingAsync_EventArrivesBeforeTheAnswer_ReturnsTheAnswer()
    {
        // Arrange
        var (port, fakeVice) = StartFakeVice(async stream =>
        {
            var (requestId, _) = await ReadCommandAsync(stream);
            await stream.WriteAsync(StoppedEvent);
            await stream.WriteAsync(Answer(ResponseType.Ping, ResponseError.Ok, requestId, []));
        });
        await using var client = await MonitorClient.ConnectAsync("127.0.0.1", port);

        // Act
        var response = await client.PingAsync().WaitAsync(Timeout);

        // Assert
        await fakeVice;
        Assert.Equal(ResponseType.Ping, response.Type);
        Assert.Equal(ResponseError.Ok, response.Error);
        Assert.False(response.IsEvent);
    }

    [Fact]
    public async Task PingAsync_ConnectionClosedBeforeTheAnswer_ThrowsIOException()
    {
        // Arrange
        var (port, fakeVice) = StartFakeVice(async stream =>
        {
            await ReadCommandAsync(stream);
            // Close without answering.
        });
        await using var client = await MonitorClient.ConnectAsync("127.0.0.1", port);

        // Act
        var exception = await Record.ExceptionAsync(() => client.PingAsync().WaitAsync(Timeout));

        // Assert
        await fakeVice;
        Assert.IsType<IOException>(exception);
    }

    [Fact]
    public async Task Events_StoppedEventArrives_IsPassedOnWithItsBody()
    {
        // Arrange
        var (port, fakeVice) = StartFakeVice(async stream =>
        {
            var (requestId, _) = await ReadCommandAsync(stream);
            await stream.WriteAsync(StoppedEvent);
            await stream.WriteAsync(Answer(ResponseType.Ping, ResponseError.Ok, requestId, []));
        });
        await using var client = await MonitorClient.ConnectAsync("127.0.0.1", port);
        await client.PingAsync().WaitAsync(Timeout);

        // Act
        var received = await client.Events.ReadAsync().AsTask().WaitAsync(Timeout);

        // Assert
        await fakeVice;
        Assert.Equal(ResponseType.Stopped, received.Type);
        Assert.True(received.IsEvent);
        Assert.Equal([0xCF, 0xE5], received.Body.ToArray());
    }

    // Starts a fake VICE on a free local port. It accepts one connection and runs the script on it.
    private static (int Port, Task Done) StartFakeVice(Func<NetworkStream, Task> script)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var done = Task.Run(async () =>
        {
            try
            {
                using var connection = await listener.AcceptTcpClientAsync();
                await script(connection.GetStream());
            }
            finally
            {
                listener.Stop();
            }
        });

        return (port, done);
    }

    // Reads one command and returns its request ID bytes and its body.
    private static async Task<(byte[] RequestId, byte[] Body)> ReadCommandAsync(NetworkStream stream)
    {
        var header = new byte[MonitorCommand.HeaderLength];
        await stream.ReadExactlyAsync(header);

        var body = new byte[BitConverter.ToInt32(header, 2)];
        await stream.ReadExactlyAsync(body);

        return (header[6..10], body);
    }

    // Builds a response with the given request ID, as VICE would send it.
    private static byte[] Answer(ResponseType type, ResponseError error, byte[] requestId, byte[] body) =>
        [0x02, 0x02, (byte)body.Length, (byte)(body.Length >> 8), 0x00, 0x00, (byte)type, (byte)error, .. requestId, .. body];
}
