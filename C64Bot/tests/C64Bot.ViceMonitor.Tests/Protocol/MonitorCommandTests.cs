using C64Bot.ViceMonitor.Protocol;

namespace C64Bot.ViceMonitor.Tests.Protocol;

public class MonitorCommandTests
{
    [Fact]
    public void Encode_ManualCheckpointExample_MatchesManualBytes()
    {
        // Arrange
        // Section 13.3 of the VICE manual: a temporary exec checkpoint on $FCE2-$FCE3.
        byte[] body = [0xE2, 0xFC, 0xE3, 0xFC, 0x01, 0x01, 0x04, 0x01];
        var command = new MonitorCommand(CommandType.CheckpointSet, 0x1234DEAD, body);

        byte[] expected =
        [
            0x02, 0x02,             // STX, API version
            0x08, 0x00, 0x00, 0x00, // body length: 8
            0xAD, 0xDE, 0x34, 0x12, // request ID 0x1234DEAD, little-endian
            0x12,                   // command type: checkpoint set
            0xE2, 0xFC, 0xE3, 0xFC, 0x01, 0x01, 0x04, 0x01, // body
        ];
        
        // Act
        var actual = command.Encode();

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Encode_PingWithEmptyBody_IsJustTheHeader()
    {
        // Arrange
        var command = new MonitorCommand(CommandType.Ping, 1, ReadOnlyMemory<byte>.Empty);

        byte[] expected =
        [
            0x02, 0x02,             // STX, API version
            0x00, 0x00, 0x00, 0x00, // body length: 0
            0x01, 0x00, 0x00, 0x00, // request ID 1, little-endian
            0x81,                   // command type: ping
        ];

        // Act
        var actual = command.Encode();

        // Assert
        Assert.Equal(expected, actual);
    }
}
