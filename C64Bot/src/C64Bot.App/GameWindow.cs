using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace C64Bot.App;

/// <summary>
/// A window that shows the emulator's picture, scaled up with sharp pixels.
/// </summary>
public sealed class GameWindow : Window
{
    private readonly FrameBuffer _frames;
    private readonly Image _image = new() { Stretch = Stretch.Uniform };
    private WriteableBitmap? _bitmap;
    private long _shownVersion = -1;

    public GameWindow(FrameBuffer frames)
    {
        _frames = frames;
        Title = "C64Bot";
        Width = 800;
        Height = 600;
        Background = Brushes.Black;

        // Scaling up a C64 picture should give bigger square pixels, not a blur.
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);
        Content = _image;

        CompositionTarget.Rendering += OnRendering;
        Closed += (_, _) => CompositionTarget.Rendering -= OnRendering;
    }

    // WPF calls this every time it is about to draw the screen, usually 60 times a second.
    private void OnRendering(object? sender, EventArgs e)
    {
        var version = _frames.Version;
        if (version == _shownVersion)
            return;

        _shownVersion = version;
        _frames.Read((pixels, width, height) =>
        {
            if (width == 0)
                return;

            if (_bitmap is null || _bitmap.PixelWidth != width || _bitmap.PixelHeight != height)
            {
                _bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr32, null);
                _image.Source = _bitmap;
            }

            _bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);
        });
    }
}
