namespace C64Bot.Libretro.Tests;

public class PrgFileTests
{
    [Fact]
    public void Load_ProgramFile_ReadsLoadAddressLowByteFirstAndData()
    {
        // Arrange
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, [0x00, 0x60, 0xEE, 0x2D, 0x13]);

        try
        {
            // Act
            var prg = PrgFile.Load(path);

            // Assert
            Assert.Equal(0x6000, prg.LoadAddress);
            Assert.Equal([0xEE, 0x2D, 0x13], prg.Data);
            Assert.Equal(0x6000..0x6003, prg.Range);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_FileShorterThanALoadAddress_ThrowsInvalidDataException()
    {
        // Arrange
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, [0x00]);

        try
        {
            // Act
            var exception = Record.Exception(() => PrgFile.Load(path));

            // Assert
            Assert.IsType<InvalidDataException>(exception);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ToRamImage_PutsTheDataAtItsLoadAddressAndZerosElsewhere()
    {
        // Arrange
        var prg = new PrgFile(0x0801, [0x0B, 0x08]);

        // Act
        var image = prg.ToRamImage();

        // Assert
        Assert.Equal(0x10000, image.Length);
        Assert.Equal(0x0B, image[0x0801]);
        Assert.Equal(0x08, image[0x0802]);
        Assert.Equal(0, image[0x0800]);
        Assert.Equal(0, image[0x0803]);
    }

    [Fact]
    public void ToRamImage_ProgramBeyondTheImage_ThrowsArgumentException()
    {
        // Arrange
        var prg = new PrgFile(0xFFFF, [1, 2]);

        // Act
        var exception = Record.Exception(() => prg.ToRamImage());

        // Assert
        Assert.IsType<ArgumentException>(exception);
    }
}
