namespace C64Bot.App;

/// <summary>
/// The newest picture from the emulator, passed from the emulation thread to the window.
/// </summary>
/// <remarks>
/// The emulation thread stores every new picture, and the window copies out the newest one when it redraws.
/// Pictures the window doesn't get to are overwritten, so a slow window never holds up the emulation.
/// </remarks>
public sealed class FrameBuffer
{
    private readonly Lock _lock = new();
    private byte[] _pixels = [];
    private int _width;
    private int _height;
    private long _version;

    /// <summary>
    /// Counts up with every stored picture, so the window can tell whether there's a new one.
    /// </summary>
    public long Version
    {
        get
        {
            lock (_lock)
                return _version;
        }
    }

    /// <summary>
    /// Copies a picture in, row by row, leaving out any padding at the end of each row.
    /// </summary>
    public void Store(ReadOnlySpan<byte> pixels, int width, int height, int pitch)
    {
        lock (_lock)
        {
            var rowLength = width * 4;
            if (_pixels.Length != rowLength * height)
                _pixels = new byte[rowLength * height];

            for (var row = 0; row < height; row++)
                pixels.Slice(row * pitch, rowLength).CopyTo(_pixels.AsSpan(row * rowLength));

            _width = width;
            _height = height;
            _version++;
        }
    }

    /// <summary>
    /// Lets <paramref name="read"/> look at the newest picture: the pixels (4 bytes each, no padding), the width
    /// and the height. The picture can't change during the call.
    /// </summary>
    public void Read(Action<byte[], int, int> read)
    {
        lock (_lock)
            read(_pixels, _width, _height);
    }
}
