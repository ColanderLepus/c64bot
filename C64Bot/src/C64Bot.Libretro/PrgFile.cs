namespace C64Bot.Libretro;

/// <summary>
/// A C64 program file (<c>.prg</c>): a 2-byte load address, low byte first, followed by the bytes that go there.
/// </summary>
/// <remarks>
/// This is how the C64 stores programs on disk, and how tools like VICE's <c>c1541</c> extract them. Comparing
/// one with the RAM shows whether the program in memory is still the one in the file.
/// </remarks>
public sealed record PrgFile(int LoadAddress, byte[] Data)
{
    /// <summary>
    /// The addresses the program covers once loaded.
    /// </summary>
    public Range Range => LoadAddress..(LoadAddress + Data.Length);

    /// <exception cref="InvalidDataException">The file is too short to hold a load address.</exception>
    public static PrgFile Load(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 2)
            throw new InvalidDataException($"{path} is too short for a program file: {bytes.Length} bytes.");

        return new PrgFile(bytes[0] | bytes[1] << 8, bytes[2..]);
    }

    /// <summary>
    /// Makes a RAM image with the program at its load address and zeros everywhere else.
    /// </summary>
    /// <param name="size">The size of the image; the C64's 64 KB unless given.</param>
    /// <exception cref="ArgumentException">The program doesn't fit in an image of this size.</exception>
    public RamImage ToRamImage(int size = 0x10000)
    {
        if (LoadAddress + Data.Length > size)
            throw new ArgumentException(
                $"The program ends at ${LoadAddress + Data.Length:X4}, beyond an image of {size} bytes.", nameof(size));

        var bytes = new byte[size];
        Data.CopyTo(bytes, LoadAddress);
        return new RamImage(bytes);
    }
}
