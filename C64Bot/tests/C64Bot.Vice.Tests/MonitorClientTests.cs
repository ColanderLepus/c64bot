using System.Net;
using System.Net.Sockets;
using C64Bot.Vice.Protocol;

namespace C64Bot.Vice.Tests;

public class MonitorClientTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task PingAsync_EventArrivesBeforeTheAnswer_ReturnsTheAnswer()
    {
        // Arrange
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var fakeVice = Task.Run(async () =>
        {
            using var connection = await listener.AcceptTcpClientAsync();
            var stream = connection.GetStream();

            var command = new byte[MonitorCommand.HeaderLength];
            await stream.ReadExactlyAsync(command);
            var requestId = command[6..10];

            byte[] stoppedEvent = [0x02, 0x02, 0x02, 0x00, 0x00, 0x00, 0x62, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xCF, 0xE5];
            byte[] pingAnswer = [0x02, 0x02, 0x00, 0x00, 0x00, 0x00, 0x81, 0x00, .. requestId];
            await stream.WriteAsync(stoppedEvent);
            await stream.WriteAsync(pingAnswer);
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
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var fakeVice = Task.Run(async () =>
        {
            using var connection = await listener.AcceptTcpClientAsync();
            var command = new byte[MonitorCommand.HeaderLength];
            await connection.GetStream().ReadExactlyAsync(command);
            // Close without answering.
        });

        await using var client = await MonitorClient.ConnectAsync("127.0.0.1", port);

        // Act
        var exception = await Record.ExceptionAsync(() => client.PingAsync().WaitAsync(Timeout));

        // Assert
        Assert.IsType<IOException>(exception);
        await fakeVice;
    }
}
