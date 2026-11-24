using System.Collections.Immutable;

namespace C64Bot.Libretro;

/// <summary>
/// A list of changes to make to the emulated machine's RAM: bytes, and the addresses they go to.
/// </summary>
/// <remarks>
/// A patch never changes. It's built by chaining, each call giving a new patch with one more change:
/// <c>RamPatch.Empty.Write(0x1305, 5).Write(0x1000, level)</c>. It knows nothing about any game; what the bytes mean
/// is up to whoever builds it.
/// </remarks>
public sealed class RamPatch
{
    private readonly ImmutableList<(int Address, byte[] Bytes)> _writes;

    private RamPatch(ImmutableList<(int Address, byte[] Bytes)> writes) => _writes = writes;

    /// <summary>
    /// The patch that changes nothing, to build others from.
    /// </summary>
    public static RamPatch Empty { get; } = new([]);

    /// <summary>
    /// Gives a patch that does what this one does and then writes these bytes, over earlier ones where they overlap.
    /// </summary>
    /// <param name="address">Where the first byte goes.</param>
    /// <param name="bytes">The bytes. They're copied, so changing them later doesn't change the patch.</param>
    /// <exception cref="ArgumentOutOfRangeException">The address is negative.</exception>
    public RamPatch Write(int address, params ReadOnlySpan<byte> bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(address);
        return new RamPatch(_writes.Add((address, bytes.ToArray())));
    }

    /// <summary>
    /// Writes the changes into the machine's RAM, in the order they were added (see
    /// <see cref="LibretroCore.WriteSystemRam"/>).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Some bytes don't fit in RAM. The changes before them have been made.
    /// </exception>
    /// <exception cref="InvalidOperationException">No game has been loaded.</exception>
    /// <exception cref="ObjectDisposedException">The core has been disposed.</exception>
    public void ApplyTo(LibretroCore core)
    {
        ArgumentNullException.ThrowIfNull(core);
        foreach (var (address, bytes) in _writes)
            core.WriteSystemRam(address, bytes);
    }
}
