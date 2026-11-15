using System.Diagnostics;

namespace C64Bot.Libretro.Tests;

public class FrameClockTests
{
    [Fact]
    public void WaitForNextFrame_FiftyFramesPerSecond_Takes500MillisecondsFor25Frames()
    {
        // Arrange
        var stopwatch = Stopwatch.StartNew();
        using var clock = new FrameClock(50);

        // Act
        for (var i = 0; i < 25; i++)
            clock.WaitForNextFrame();
        stopwatch.Stop();

        // Assert
        // Frame 25 is due 500 ms after the clock started. The upper limit leaves room for a busy PC.
        Assert.InRange(stopwatch.Elapsed.TotalMilliseconds, 495, 600);
    }

    [Fact]
    public void WaitForNextFrame_AfterFallingFarBehind_DoesNotRaceToCatchUp()
    {
        // Arrange
        using var clock = new FrameClock(50);
        clock.WaitForNextFrame();
        Thread.Sleep(300); // far more than the 100 ms the clock lets itself fall behind
        var stopwatch = Stopwatch.StartNew();

        // Act
        for (var i = 0; i < 5; i++)
            clock.WaitForNextFrame();
        stopwatch.Stop();

        // Assert
        // The clock starts counting again: the first frame comes at once, the other four 20 ms apart. Racing to
        // catch up would take next to no time.
        Assert.InRange(stopwatch.Elapsed.TotalMilliseconds, 75, 200);
    }

    [Fact]
    public void WaitForNextFrame_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var clock = new FrameClock(50);
        clock.Dispose();

        // Act
        var exception = Record.Exception(clock.WaitForNextFrame);

        // Assert
        Assert.IsType<ObjectDisposedException>(exception);
    }

    [Fact]
    public void Dispose_Twice_DoesNotThrow()
    {
        // Arrange
        var clock = new FrameClock(50);
        clock.Dispose();

        // Act
        var exception = Record.Exception(clock.Dispose);

        // Assert
        Assert.Null(exception);
    }
}
