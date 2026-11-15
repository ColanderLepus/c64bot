namespace C64Bot.Libretro;

/// <summary>
/// A copy of the emulated machine's RAM at one moment, for comparing with another copy to find out where a game
/// keeps its data.
/// </summary>
public sealed class RamImage
{
    private readonly byte[] _bytes;

    /// <summary>
    /// Copies the bytes, for example from <see cref="LibretroCore.SystemRam"/>, so the image stays as it is while
    /// the game runs on.
    /// </summary>
    public RamImage(ReadOnlySpan<byte> bytes) => _bytes = bytes.ToArray();

    public int Length => _bytes.Length;

    public ReadOnlySpan<byte> Bytes => _bytes;

    public byte this[int address] => _bytes[address];

    /// <summary>
    /// Reads an image saved with <see cref="Save"/>: the raw bytes, nothing else.
    /// </summary>
    public static RamImage Load(string path) => new(File.ReadAllBytes(path));

    /// <summary>
    /// Writes the image to a file as raw bytes, the first byte being address 0.
    /// </summary>
    public void Save(string path) => File.WriteAllBytes(path, _bytes);

    /// <summary>
    /// Finds the bytes that differ between this image and a later one, as runs of consecutive addresses in
    /// address order.
    /// </summary>
    /// <param name="later">The image to compare with; it must be the same size.</param>
    /// <param name="range">The addresses to compare, for example <c>0x6000..0xB800</c>. All of them if left out.</param>
    /// <exception cref="ArgumentException">The images aren't the same size.</exception>
    public IReadOnlyList<RamChange> Compare(RamImage later, Range? range = null)
    {
        ArgumentNullException.ThrowIfNull(later);
        if (later.Length != Length)
            throw new ArgumentException($"The images differ in size: {Length} and {later.Length} bytes.", nameof(later));

        var (start, length) = (range ?? Range.All).GetOffsetAndLength(Length);
        var end = start + length;

        var changes = new List<RamChange>();
        var address = start;
        while (address < end)
        {
            if (_bytes[address] == later._bytes[address])
            {
                address++;
                continue;
            }

            var runStart = address;
            while (address < end && _bytes[address] != later._bytes[address])
                address++;

            changes.Add(new RamChange(runStart, _bytes[runStart..address], later._bytes[runStart..address]));
        }

        return changes;
    }
}
