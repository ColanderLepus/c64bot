namespace C64Bot.Libretro.Tests;

public class RamImageTests
{
    [Fact]
    public void Compare_IdenticalImages_ReturnsNoChanges()
    {
        // Arrange
        var earlier = new RamImage([1, 2, 3, 4]);
        var later = new RamImage([1, 2, 3, 4]);

        // Act
        var changes = earlier.Compare(later);

        // Assert
        Assert.Empty(changes);
    }

    [Fact]
    public void Compare_ConsecutiveChanges_ReturnsOneRunWithBothSidesBytes()
    {
        // Arrange
        var earlier = new RamImage([0, 1, 2, 3, 0]);
        var later = new RamImage([0, 7, 8, 9, 0]);

        // Act
        var changes = earlier.Compare(later);

        // Assert
        var change = Assert.Single(changes);
        Assert.Equal(1, change.Start);
        Assert.Equal(4, change.End);
        Assert.Equal([1, 2, 3], change.Before);
        Assert.Equal([7, 8, 9], change.After);
    }

    [Fact]
    public void Compare_SeparateChanges_ReturnsSeparateRunsInAddressOrder()
    {
        // Arrange
        var earlier = new RamImage([1, 0, 0, 2, 0, 3]);
        var later = new RamImage([9, 0, 0, 8, 0, 7]);

        // Act
        var changes = earlier.Compare(later);

        // Assert
        Assert.Equal([0, 3, 5], changes.Select(change => change.Start));
    }

    [Fact]
    public void Compare_WithRange_IgnoresChangesOutsideIt()
    {
        // Arrange
        var earlier = new RamImage([1, 2, 3, 4, 5, 6]);
        var later = new RamImage([9, 9, 9, 9, 9, 9]);

        // Act
        var changes = earlier.Compare(later, 2..4);

        // Assert
        var change = Assert.Single(changes);
        Assert.Equal(2, change.Start);
        Assert.Equal([3, 4], change.Before);
    }

    [Fact]
    public void Compare_ImagesOfDifferentSizes_ThrowsArgumentException()
    {
        // Arrange
        var earlier = new RamImage([1, 2, 3]);
        var later = new RamImage([1, 2]);

        // Act
        var exception = Record.Exception(() => earlier.Compare(later));

        // Assert
        Assert.IsType<ArgumentException>(exception);
    }

    [Fact]
    public void Constructor_SourceChangedAfterwards_KeepsTheCopiedBytes()
    {
        // Arrange
        byte[] source = [1, 2, 3];
        var image = new RamImage(source);

        // Act
        source[1] = 99;

        // Assert
        Assert.Equal(2, image[1]);
    }

    [Fact]
    public void Load_AfterSave_GivesTheSameBytes()
    {
        // Arrange
        var path = Path.GetTempFileName();
        var image = new RamImage([5, 0, 255, 17]);

        try
        {
            // Act
            image.Save(path);
            var loaded = RamImage.Load(path);

            // Assert
            Assert.Equal(image.Bytes.ToArray(), loaded.Bytes.ToArray());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
