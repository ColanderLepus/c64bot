namespace C64Bot.Libretro.Tests;

// Tests that don't need the VICE core.
public class LibretroCoreTests
{
    [Fact]
    public void Load_MissingFile_ThrowsDllNotFoundException()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), "no-such-core.dll");

        // Act
        var exception = Record.Exception(() => LibretroCore.Load(path));

        // Assert
        Assert.IsType<DllNotFoundException>(exception);
    }

    [Fact]
    public void Load_DllThatIsNotACore_ThrowsEntryPointNotFoundException()
    {
        // Arrange
        // A Windows DLL that's always there and has none of the libretro functions.
        const string path = "kernel32.dll";

        // Act
        var exception = Record.Exception(() => LibretroCore.Load(path));

        // Assert
        Assert.IsType<EntryPointNotFoundException>(exception);
    }

    [Theory]
    [InlineData("Joystick Port; 2|1", "2")]
    [InlineData("Colors; 16bit|24bit", "16bit")]
    [InlineData("Turbo Fire; disabled", "disabled")]
    public void ParseDefaultValue_DescriptionAndChoices_ReturnsTheFirstChoice(string value, string expected)
    {
        // Act
        var defaultValue = LibretroCore.ParseDefaultValue(value);

        // Assert
        Assert.Equal(expected, defaultValue);
    }

    [Fact]
    public void ParseDefaultValue_NoDescription_ReturnsTheFirstChoice()
    {
        // Act
        var defaultValue = LibretroCore.ParseDefaultValue("2|1");

        // Assert
        Assert.Equal("2", defaultValue);
    }

    [Fact]
    public void ParseDefaultValue_SemicolonWithoutSpaceInTheDescription_SplitsAtSemicolonAndSpace()
    {
        // Act
        var defaultValue = LibretroCore.ParseDefaultValue("Ports 1;2; on|off");

        // Assert
        Assert.Equal("on", defaultValue);
    }
}
