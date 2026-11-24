namespace C64Bot.Libretro.Tests;

public sealed class DiskImageTests : IDisposable
{
    private const int Size35Tracks = 174_848;
    private const int Size35TracksWithErrorCodes = 175_531;
    private const int Size40Tracks = 196_608;
    private const int Size40TracksWithErrorCodes = 197_376;

    private readonly string _path = Path.GetTempFileName();

    public void Dispose() => File.Delete(_path);

    [Theory]
    [InlineData(Size35Tracks, 35)]
    [InlineData(Size35TracksWithErrorCodes, 35)]
    [InlineData(Size40Tracks, 40)]
    [InlineData(Size40TracksWithErrorCodes, 40)]
    public void Load_SizeOfADisk_FindsTheNumberOfTracks(int size, int expectedTracks)
    {
        // Arrange
        File.WriteAllBytes(_path, new byte[size]);

        // Act
        var disk = DiskImage.Load(_path);

        // Assert
        Assert.Equal(expectedTracks, disk.TrackCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(Size35Tracks - 1)]
    [InlineData(Size35Tracks + 1)]
    public void Load_OtherSize_ThrowsInvalidDataException(int size)
    {
        // Arrange
        File.WriteAllBytes(_path, new byte[size]);

        // Act
        var exception = Record.Exception(() => DiskImage.Load(_path));

        // Assert
        Assert.IsType<InvalidDataException>(exception);
    }

    [Fact]
    public void Load_FileOpenForWritingElsewhere_ReadsIt()
    {
        // Arrange
        File.WriteAllBytes(_path, new byte[Size35Tracks]);
        using var other = new FileStream(_path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

        // Act
        var disk = DiskImage.Load(_path);

        // Assert
        Assert.Equal(35, disk.TrackCount);
    }

    // The offsets are the ones in the usual .d64 tables: the first and last sector of each zone of tracks.
    [Theory]
    [InlineData(1, 0, 0x00000)]
    [InlineData(17, 20, 0x16400)]
    [InlineData(18, 0, 0x16500)]
    [InlineData(24, 18, 0x1E900)]
    [InlineData(25, 0, 0x1EA00)]
    [InlineData(30, 17, 0x25500)]
    [InlineData(31, 0, 0x25600)]
    [InlineData(35, 16, 0x2AA00)]
    public void ReadSector_GivesThe256BytesAtTheSectorsPlace(int track, int sector, int offset)
    {
        // Arrange
        var bytes = new byte[Size35Tracks];
        var expected = Enumerable.Range(1, DiskImage.SectorSize).Select(i => (byte)(i % 251 + 1)).ToArray();
        expected.CopyTo(bytes, offset);
        File.WriteAllBytes(_path, bytes);
        var disk = DiskImage.Load(_path);

        // Act
        var actual = disk.ReadSector(track, sector).ToArray();

        // Assert
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(36, 0)]
    [InlineData(1, -1)]
    [InlineData(17, 21)]
    [InlineData(18, 19)]
    [InlineData(25, 18)]
    [InlineData(35, 17)]
    public void ReadSector_NoSuchTrackOrSector_ThrowsArgumentOutOfRangeException(int track, int sector)
    {
        // Arrange
        File.WriteAllBytes(_path, new byte[Size35Tracks]);
        var disk = DiskImage.Load(_path);

        // Act
        var exception = Record.Exception(() => { disk.ReadSector(track, sector); });

        // Assert
        Assert.IsType<ArgumentOutOfRangeException>(exception);
    }

    // Lode Runner keeps its levels from track 3 on; level 1 is sector 0, whose tiles are bytes 1-224 (see
    // docs/lode-runner.md). The player is tile code 9.
    [CoreFact]
    public void ReadSector_LodeRunnersFirstLevel_HasOnePlayer()
    {
        // Arrange
        var disk = DiskImage.Load(CoreFiles.DiskPath);

        // Act
        var tiles = disk.ReadSector(3, 0).Slice(1, 224).ToArray();

        // Assert
        Assert.Equal(1, tiles.Sum(pair => (pair & 0x0F) == 9 ? 1 : 0) + tiles.Sum(pair => pair >> 4 == 9 ? 1 : 0));
    }
}
