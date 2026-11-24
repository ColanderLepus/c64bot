using C64Bot.Libretro;
using C64Bot.Libretro.Tests;

namespace C64Bot.LodeRunner.Tests;

public class LevelDiskTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(151)]
    public void Indexer_NoSuchLevel_ThrowsArgumentOutOfRangeException(int level)
    {
        // Arrange
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, new byte[174_848]);

        try
        {
            var levels = new LevelDisk(DiskImage.Load(path));

            // Act
            var exception = Record.Exception(() => levels[level]);

            // Assert
            Assert.IsType<ArgumentOutOfRangeException>(exception);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [CoreFact]
    public void Indexer_EveryLevel_GivesBackTheBytesOnTheDisk()
    {
        // Arrange
        var disk = DiskImage.Load(CoreFiles.DiskPath);
        var levels = new LevelDisk(disk);
        var onDisk = AllLevels().Select(level =>
            disk.ReadSector((level - 1) / 16 + 3, (level - 1) % 16).Slice(1, 224).ToArray());

        // Act
        var bytes = AllLevels().Select(level => levels[level].ToBytes());

        // Assert
        Assert.Equal(onDisk, bytes);
    }

    // The numbers are the ones in docs/lode-runner.md ("Guards per level").
    [CoreFact]
    public void Indexer_EveryLevel_HasTheGuardsItShould()
    {
        // Arrange
        var disk = DiskImage.Load(CoreFiles.DiskPath);
        var levels = new LevelDisk(disk);

        // Act
        var levelsByGuards = AllLevels()
            .Select(level => levels[level])
            .CountBy(level => Math.Min(5, Positions().Count(position => level[position] == Tile.Guard)))
            .OrderBy(pair => pair.Key)
            .Select(pair => (pair.Key, pair.Value));

        // Assert
        Assert.Equal([(2, 16), (3, 70), (4, 47), (5, 17)], levelsByGuards);
    }

    [CoreFact]
    public void Indexer_EveryLevel_HasItsNumber()
    {
        // Arrange
        var disk = DiskImage.Load(CoreFiles.DiskPath);
        var levels = new LevelDisk(disk);

        // Act
        var numbers = AllLevels().Select(level => levels[level].Number);

        // Assert
        Assert.Equal(AllLevels(), numbers);
    }

    private static IEnumerable<int> AllLevels() => Enumerable.Range(1, Level.MaxNumber);

    private static IEnumerable<TilePosition> Positions() =>
        from row in Enumerable.Range(0, Level.Height)
        from column in Enumerable.Range(0, Level.Width)
        select new TilePosition(column, row);
}
