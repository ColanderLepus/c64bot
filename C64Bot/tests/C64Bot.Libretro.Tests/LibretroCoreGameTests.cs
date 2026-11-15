namespace C64Bot.Libretro.Tests;

// Tests on the shared core, which has run Lode Runner to its trainer menu. Only the PressKey test moves the menu
// on, and the other tests don't depend on how far it is.
[Collection(CoreCollection.Name)]
public class LibretroCoreGameTests(LodeRunnerAtTheTrainerMenu lodeRunner)
{
    [CoreFact]
    public void SystemRam_AtTheTrainerMenu_HoldsTheMenuText()
    {
        // Act
        var length = lodeRunner.Core.SystemRam.Length;
        var screen = lodeRunner.ScreenText();

        // Assert
        Assert.Equal(65536, length);
        Assert.Contains("UNLIMITED LIVES (Y/N) ?", screen);
    }

    [CoreFact]
    public void Options_AfterInitialize_HoldTheValuesSetAndTheDefaults()
    {
        // Act
        var options = lodeRunner.Core.Options;

        // Assert
        Assert.Equal("24bit", options["vice_gfx_colors"]);
        Assert.Equal("2", options["vice_joyport"]);
        Assert.Equal("disabled", options["vice_turbo_fire"]);
    }

    [CoreFact]
    public void RunFrame_AtTheTrainerMenu_SendsAPictureOf384By272()
    {
        // Arrange
        var core = lodeRunner.Core;
        (int Width, int Height, int Pitch, int Length)? picture = null;
        core.VideoFrame = (pixels, width, height, pitch) => picture = (width, height, pitch, pixels.Length);

        // Act
        core.RunFrame();
        core.VideoFrame = null;

        // Assert
        Assert.NotNull(picture);
        Assert.Equal(384, picture.Value.Width);
        Assert.Equal(272, picture.Value.Height);
        Assert.True(picture.Value.Pitch >= 384 * 4);
        Assert.Equal(picture.Value.Pitch * 271 + 384 * 4, picture.Value.Length);
    }

    [CoreFact]
    public void PressKey_YAtTheFirstQuestion_BringsUpTheSecondQuestion()
    {
        // Arrange
        var core = lodeRunner.Core;

        // Act
        core.PressKey(RetroKey.Y);
        RunFrames(5);
        core.ReleaseKey(RetroKey.Y);
        RunFrames(5);

        // Assert
        Assert.Contains("TOTAL INVINCIBILITY (Y/N) ?", lodeRunner.ScreenText());
    }

    private void RunFrames(int frames)
    {
        for (var i = 0; i < frames; i++)
            lodeRunner.Core.RunFrame();
    }
}
