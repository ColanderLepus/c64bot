namespace C64Bot.Libretro.Tests;

// Save states on the shared core, which has run Lode Runner to its trainer menu. Each test loads back the state it
// found before it ends, so the other tests still find the menu as they expect.
[Collection(CoreCollection.Name)]
public class LibretroCoreStateTests(LodeRunnerAtTheTrainerMenu lodeRunner)
{
    private LibretroCore Core => lodeRunner.Core;

    [CoreFact]
    public void LoadState_AfterRunningOn_BringsBackTheRam()
    {
        // Arrange
        var state = Core.SaveState();
        try
        {
            var ramBefore = Core.SystemRam.ToArray();
            for (var i = 0; i < 100; i++)
                Core.RunFrame();
            var ramAfterRunning = Core.SystemRam.ToArray();

            // Act
            Core.LoadState(state);
            var ramAfterLoading = Core.SystemRam.ToArray();

            // Assert
            Assert.NotEqual(ramBefore, ramAfterRunning);
            Assert.Equal(ramBefore, ramAfterLoading);
        }
        finally
        {
            Core.LoadState(state);
        }
    }

    [CoreFact]
    public void LoadState_KeyHeldBeforeTheLoad_IsLetGoOf()
    {
        // The KERNAL's modifier keys held, updated by its keyboard scan: bit 0 Shift, bit 1 C=.
        const int ModifierKeys = 0x028D;

        // Arrange
        var state = Core.SaveState();
        try
        {
            Core.PressKey(RetroKey.LeftShift);
            RunFrames(5);
            var modifiersBeforeTheLoad = Core.SystemRam[ModifierKeys];

            // Act
            Core.LoadState(state);
            Core.PressKey(RetroKey.LeftCtrl); // C= on the C64; any key event makes VICE update the C64's keyboard
            RunFrames(10);
            var modifiersAfterTheLoad = Core.SystemRam[ModifierKeys];

            // Assert
            Assert.Equal(1, modifiersBeforeTheLoad);
            Assert.Equal(2, modifiersAfterTheLoad); // C= only: Shift was let go of by the load
        }
        finally
        {
            Core.LoadState(state);
        }
    }

    [CoreFact]
    public void LoadState_EmptyState_ThrowsArgumentException()
    {
        // Act
        var exception = Record.Exception(() => Core.LoadState([]));

        // Assert
        Assert.IsType<ArgumentException>(exception);
    }

    [CoreFact]
    public void LoadState_NotASaveState_ThrowsInvalidOperationException()
    {
        // Arrange
        var state = Core.SaveState();
        var notAState = new byte[1000];
        try
        {
            // Act
            var exception = Record.Exception(() => Core.LoadState(notAState));

            // Assert
            Assert.IsType<InvalidOperationException>(exception);
        }
        finally
        {
            Core.LoadState(state);
        }
    }

    private void RunFrames(int frames)
    {
        for (var i = 0; i < frames; i++)
            Core.RunFrame();
    }
}
