namespace C64Bot.Libretro;

/// <summary>
/// A copy of a C64 floppy disk, read from a disk image file (<c>.d64</c>).
/// </summary>
/// <remarks>
/// A disk is divided into tracks, numbered from 1, and each track into sectors of 256 bytes, numbered from 0. The
/// outer tracks are longer and hold more sectors: 21 on tracks 1–17, 19 on 18–24, 18 on 25–30 and 17 from 31 on. A
/// <c>.d64</c> file holds all the sectors one after another, sometimes followed by one error code per sector, which
/// is ignored here.
/// </remarks>
public sealed class DiskImage
{
    public const int SectorSize = 256;

    private readonly byte[] _bytes;

    private DiskImage(byte[] bytes, int trackCount)
    {
        _bytes = bytes;
        TrackCount = trackCount;
    }

    /// <summary>
    /// The number of tracks on the disk: 35, or 40 for an extended disk.
    /// </summary>
    public int TrackCount { get; }

    /// <summary>
    /// Reads a disk image file, also while another program has it open.
    /// </summary>
    /// <exception cref="InvalidDataException">The file doesn't have the size of a disk image.</exception>
    public static DiskImage Load(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        foreach (var trackCount in new[] { 35, 40 })
        {
            var sectors = SectorsBefore(trackCount + 1);
            if (file.Length != sectors * SectorSize && file.Length != sectors * (SectorSize + 1))
                continue;

            var bytes = new byte[sectors * SectorSize];
            file.ReadExactly(bytes);
            return new DiskImage(bytes, trackCount);
        }

        throw new InvalidDataException($"{path} doesn't have the size of a disk image: {file.Length} bytes.");
    }

    /// <param name="track">The track, 1 to <see cref="TrackCount"/>.</param>
    /// <param name="sector">The sector, from 0; how many there are depends on the track.</param>
    /// <returns>The sector's <see cref="SectorSize"/> bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The disk has no such track or sector.</exception>
    public ReadOnlySpan<byte> ReadSector(int track, int sector)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(track, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(track, TrackCount);
        ArgumentOutOfRangeException.ThrowIfNegative(sector);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(sector, SectorsOn(track));

        return _bytes.AsSpan((SectorsBefore(track) + sector) * SectorSize, SectorSize);
    }

    private static int SectorsOn(int track) => track switch { <= 17 => 21, <= 24 => 19, <= 30 => 18, _ => 17 };

    private static int SectorsBefore(int track) => Enumerable.Range(1, track - 1).Sum(SectorsOn);
}
