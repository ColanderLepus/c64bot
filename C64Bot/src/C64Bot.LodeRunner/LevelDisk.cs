using C64Bot.Libretro;

namespace C64Bot.LodeRunner;

/// <summary>
/// The levels on the game's disk, by their number.
/// </summary>
/// <param name="disk">The Lode Runner disk.</param>
public sealed class LevelDisk(DiskImage disk)
{
    // Where the levels are (see "Levels on the disk" in docs/lode-runner.md).
    private const int FirstTrack = 3;
    private const int LevelsPerTrack = 16;
    private const int FirstByteInSector = 1;

    /// <param name="number">The level's number, 1 to <see cref="Level.MaxNumber"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">There's no such level.</exception>
    public Level this[int number]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(number, Level.MaxNumber);
            var (tracks, sector) = Math.DivRem(number - 1, LevelsPerTrack);
            var bytes = disk.ReadSector(FirstTrack + tracks, sector).Slice(FirstByteInSector, Level.ByteCount);
            return Level.FromBytes(number, bytes);
        }
    }
}
