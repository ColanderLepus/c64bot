using System.Net;
using System.Net.Sockets;
using C64Bot.Vice.Protocol;

namespace C64Bot.Vice.Tests;

public class MonitorClientTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    // A stopped event with PC = $E5CF, as in section 13.3 of the VICE manual.
    private static readonly byte[] StoppedEvent =
        [0x02, 0x02, 0x02, 0x00, 0x00, 0x00, 0x62, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xCF, 0xE5];

    [Fact]
    public async Task PingAsync_EventArrivesBeforeTheAnswer_ReturnsTheAnswer()
    {
        // Arrange
        var (port, fakeVice) = StartFakeVice(async stream =>
        {
            var requestId = await ReadCommandRequestIdAsync(stream);
            await stream.WriteAsync(StoppedEvent);
            await stream.WriteAsync(PingAnswer(requestId));
        });
        await using var client = await MonitorClient.ConnectAsync("127.0.0.1", port);

        // Act
        var response = await client.PingAsync().WaitAsync(Timeout);

        // Assert
        Assert.Equal(ResponseType.Ping, response.Type);
        Assert.Equal(ResponseError.Ok, response.Error);
        Assert.False(response.IsEvent);
        await fakeVice;
    }

    [Fact]
    public async Task PingAsync_ConnectionClosedBeforeTheAnswer_ThrowsIOException()
    {
        // Arrange
        var (port, fakeVice) = StartFakeVice(async stream =>
        {
            await ReadCommandRequestIdAsync(stream);
            // Close without answering.
        });
        await using var client = await MonitorClient.ConnectAsync("127.0.0.1", port);

        // Act
        var exception = await Record.ExceptionAsync(() => client.PingAsync().WaitAsync(Timeout));

        // Assert
        Assert.IsType<IOException>(exception);
        await fakeVice;
    }

    [Fact]
    public async Task Events_StoppedEventArrives_IsPassedOnWithItsBody()
    {
        // Arrange
        var (port, fakeVice) = StartFakeVice(async stream =>
        {
            var requestId = await ReadCommandRequestIdAsync(stream);
            await stream.WriteAsync(StoppedEvent);
            await stream.WriteAsync(PingAnswer(requestId));
        });
        await using var client = await MonitorClient.ConnectAsync("127.0.0.1", port);
        await client.PingAsync().WaitAsync(Timeout);

        // Act
        var received = await client.Events.ReadAsync().AsTask().WaitAsync(Timeout);

        // Assert
        Assert.Equal(ResponseType.Stopped, received.Type);
        Assert.True(received.IsEvent);
        Assert.Equal([0xCF, 0xE5], received.Body.ToArray());
        await fakeVice;
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

    // Reads one command with an empty body (such as ping) and returns its request ID bytes.
    private static async Task<byte[]> ReadCommandRequestIdAsync(NetworkStream stream)
    {
        var command = new byte[MonitorCommand.HeaderLength];
        await stream.ReadExactlyAsync(command);
        return command[6..10];
    }

    private static byte[] PingAnswer(byte[] requestId) =>
        [0x02, 0x02, 0x00, 0x00, 0x00, 0x00, 0x81, 0x00, .. requestId];
}
