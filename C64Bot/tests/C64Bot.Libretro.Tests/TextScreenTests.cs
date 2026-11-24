namespace C64Bot.Libretro.Tests;

public class TextScreenTests
{
    [Fact]
    public void Read_EachKindOfCode_GivesItsCharacter()
    {
        // Arrange
        var ram = new byte[0x10000];
        byte[] codes = [0x00, 0x01, 0x1A, 0x20, 0x30, 0x3F, 0x40]; // @ A Z space 0 ? and a graphics character
        codes.CopyTo(ram, 0x0400);

        // Act
        var text = TextScreen.Read(ram);

        // Assert
        Assert.Equal("@AZ 0?#", text[..7]);
    }

    [Fact]
    public void Read_ReversedCharacters_GivesTheSameAsTheNormalOnes()
    {
        // Arrange
        var ram = new byte[0x10000];
        (ram[0x0400], ram[0x0401]) = (0x81, 0xB1); // A and 1, reversed

        // Act
        var text = TextScreen.Read(ram);

        // Assert
        Assert.Equal("A1", text[..2]);
    }

    [Fact]
    public void Read_LastPlaceOnTheScreen_IsTheLastCharacter()
    {
        // Arrange
        var ram = new byte[0x10000];
        ram[0x07E7] = 0x1A;

        // Act
        var text = TextScreen.Read(ram);

        // Assert
        Assert.Equal(1000, text.Length);
        Assert.Equal('Z', text[^1]);
    }
}
