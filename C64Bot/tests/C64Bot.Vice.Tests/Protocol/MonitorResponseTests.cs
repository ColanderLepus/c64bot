using C64Bot.Vice.Protocol;

namespace C64Bot.Vice.Tests.Protocol;

public class MonitorResponseTests
{
    [Fact]
    public async Task ReadAsync_ManualStoppedEvent_DecodesAllFields()
    {
        // Arrange
        // Section 13.3 of the VICE manual: the stopped event VICE sends after it stops.
        byte[] bytes =
        [
            0x02, 0x02,             // STX, API version
            0x02, 0x00, 0x00, 0x00, // body length: 2
            0x62,                   // response type: stopped
            0x00,                   // error: OK
            0xFF, 0xFF, 0xFF, 0xFF, // request ID: event
            0xCF, 0xE5,             // body: PC = $E5CF
        ];
        using var stream = new MemoryStream(bytes);

        // Act
        var response = await MonitorResponse.ReadAsync(stream);

        // Assert
        Assert.Equal(ResponseType.Stopped, response.Type);
        Assert.Equal(ResponseError.Ok, response.Error);
        Assert.True(response.IsEvent);
        Assert.Equal([0xCF, 0xE5], response.Body.ToArray());
    }

    [Fact]
    public async Task ReadAsync_TwoResponsesBackToBack_ReadsThemSeparately()
    {
        // Arrange
        byte[] bytes =
        [
            // A ping response to request 7, with an empty body
            0x02, 0x02, 0x00, 0x00, 0x00, 0x00, 0x81, 0x00, 0x07, 0x00, 0x00, 0x00,
            // A stopped event with PC = $E5CF
            0x02, 0x02, 0x02, 0x00, 0x00, 0x00, 0x62, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xCF, 0xE5,
        ];
        using var stream = new MemoryStream(bytes);

        // Act
        var first = await MonitorResponse.ReadAsync(stream);
        var second = await MonitorResponse.ReadAsync(stream);

        // Assert
        Assert.Equal(ResponseType.Ping, first.Type);
        Assert.Equal(7u, first.RequestId);
        Assert.True(first.Body.IsEmpty);

        Assert.Equal(ResponseType.Stopped, second.Type);
        Assert.True(second.IsEvent);

        Assert.Equal(stream.Length, stream.Position);
    }

    [Fact]
    public async Task ReadAsync_WrongStartByte_ThrowsInvalidDataException()
    {
        // Arrange
        byte[] bytes = [0x99, 0x02, 0x00, 0x00, 0x00, 0x00, 0x81, 0x00, 0x01, 0x00, 0x00, 0x00];
        using var stream = new MemoryStream(bytes);

        // Act
        var exception = await Record.ExceptionAsync(() => MonitorResponse.ReadAsync(stream));

        // Assert
        Assert.IsType<InvalidDataException>(exception);
    }

    [Fact]
    public async Task ReadAsync_WrongApiVersion_ThrowsInvalidDataException()
    {
        // Arrange
        byte[] bytes = [0x02, 0x99, 0x00, 0x00, 0x00, 0x00, 0x81, 0x00, 0x01, 0x00, 0x00, 0x00];
        using var stream = new MemoryStream(bytes);

        // Act
        var exception = await Record.ExceptionAsync(() => MonitorResponse.ReadAsync(stream));

        // Assert
        Assert.IsType<InvalidDataException>(exception);
    }

    [Fact]
    public async Task ReadAsync_StreamEndsInsideBody_ThrowsEndOfStreamException()
    {
        // Arrange
        // The header says the body is 2 bytes long, but only 1 follows.
        byte[] bytes = [0x02, 0x02, 0x02, 0x00, 0x00, 0x00, 0x62, 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xCF];
        using var stream = new MemoryStream(bytes);

        // Act
        var exception = await Record.ExceptionAsync(() => MonitorResponse.ReadAsync(stream));

        // Assert
        Assert.IsType<EndOfStreamException>(exception);
    }
}
