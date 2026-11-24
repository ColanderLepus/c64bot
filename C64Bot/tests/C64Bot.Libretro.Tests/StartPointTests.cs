namespace C64Bot.Libretro.Tests;

// Start points and RAM patches on the shared core, which has run Lode Runner to its trainer menu. Each test goes
// back to the moment it started at before it ends, so the other tests still find the menu as they expect.
[Collection(CoreCollection.Name)]
public class StartPointTests(LodeRunnerAtTheTrainerMenu lodeRunner)
{
    // Anywhere in plain RAM will do: every test reads it back before the game runs on.
    private const int Address = 0xC100;

    private LibretroCore Core => lodeRunner.Core;

    [CoreFact]
    public void Start_AfterRunningOn_BringsBackTheSavedMoment()
    {
        // Arrange
        var startPoint = StartPoint.Save(Core);
        try
        {
            var saved = Core.SystemRam.ToArray();
            RunFrames(100);
            var afterRunning = Core.SystemRam.ToArray();

            // Act
            startPoint.Start();
            var started = Core.SystemRam.ToArray();

            // Assert
            Assert.NotEqual(saved, afterRunning);
            Assert.Equal(saved, started);
        }
        finally
        {
            startPoint.Start();
        }
    }

    [CoreFact]
    public void Start_WithAPatch_ChangesOnlyThePatchedBytes()
    {
        // Arrange
        var startPoint = StartPoint.Save(Core);
        try
        {
            var expected = Core.SystemRam.ToArray();
            byte[] bytes = [(byte)~expected[Address], (byte)~expected[Address + 1]];
            bytes.CopyTo(expected, Address);
            var patch = RamPatch.Empty.Write(Address, bytes);
            RunFrames(100);

            // Act
            startPoint.Start(patch);
            var started = Core.SystemRam.ToArray();

            // Assert
            Assert.Equal(expected, started);
        }
        finally
        {
            startPoint.Start();
        }
    }

    [CoreFact]
    public void Start_WithoutAPatchAfterOneWith_LeavesNoTraceOfThePatch()
    {
        // Arrange
        var startPoint = StartPoint.Save(Core);
        try
        {
            var saved = Core.SystemRam.ToArray();
            startPoint.Start(RamPatch.Empty.Write(Address, (byte)~saved[Address]));

            // Act
            startPoint.Start();
            var started = Core.SystemRam.ToArray();

            // Assert
            Assert.Equal(saved, started);
        }
        finally
        {
            startPoint.Start();
        }
    }

    [CoreFact]
    public void Write_OnAPatch_LeavesThatPatchAsItWas()
    {
        // Arrange
        var startPoint = StartPoint.Save(Core);
        try
        {
            var first = RamPatch.Empty.Write(Address, 1);

            // Act
            first.Write(Address, 2);

            // Assert
            startPoint.Start(first);
            Assert.Equal(1, Core.SystemRam[Address]);
        }
        finally
        {
            startPoint.Start();
        }
    }

    [CoreFact]
    public void Write_OverEarlierBytes_TheLaterOnesWin()
    {
        // Arrange
        var startPoint = StartPoint.Save(Core);
        try
        {
            // Act
            var patch = RamPatch.Empty.Write(Address, 1, 2, 3).Write(Address + 1, 9);

            // Assert
            startPoint.Start(patch);
            Assert.Equal([1, 9, 3], Core.SystemRam.Slice(Address, 3).ToArray());
        }
        finally
        {
            startPoint.Start();
        }
    }

    [CoreFact]
    public void Write_BytesChangedAfterwards_KeepsTheBytesAsTheyWere()
    {
        // Arrange
        var startPoint = StartPoint.Save(Core);
        try
        {
            byte[] bytes = [1, 2];
            var patch = RamPatch.Empty.Write(Address, bytes);

            // Act
            bytes[0] = 9;

            // Assert
            startPoint.Start(patch);
            Assert.Equal([1, 2], Core.SystemRam.Slice(Address, 2).ToArray());
        }
        finally
        {
            startPoint.Start();
        }
    }

    [CoreFact]
    public void Start_PatchBeyondTheRam_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var startPoint = StartPoint.Save(Core);
        try
        {
            var patch = RamPatch.Empty.Write(0xFFFF, 1, 2);

            // Act
            var exception = Record.Exception(() => startPoint.Start(patch));

            // Assert
            Assert.IsType<ArgumentOutOfRangeException>(exception);
        }
        finally
        {
            startPoint.Start();
        }
    }

    [Fact]
    public void Write_NegativeAddress_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var exception = Record.Exception(() => RamPatch.Empty.Write(-1, 1));

        // Assert
        Assert.IsType<ArgumentOutOfRangeException>(exception);
    }

    private void RunFrames(int frames)
    {
        for (var i = 0; i < frames; i++)
            Core.RunFrame();
    }
}
