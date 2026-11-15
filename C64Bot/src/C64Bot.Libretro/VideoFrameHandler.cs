namespace C64Bot.Libretro;

/// <summary>
/// A method that receives a finished picture from a libretro core.
/// </summary>
/// <param name="pixels">
/// The picture, row after row, 4 bytes per pixel: blue, green, red and one unused byte. Only valid during the
/// call.
/// </param>
/// <param name="pitch">
/// The number of bytes from the start of one row to the start of the next. Can be more than
/// <paramref name="width"/> × 4.
/// </param>
public delegate void VideoFrameHandler(ReadOnlySpan<byte> pixels, int width, int height, int pitch);
