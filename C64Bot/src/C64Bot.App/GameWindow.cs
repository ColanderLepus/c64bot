using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace C64Bot.App;

/// <summary>
/// A window that shows the emulator's picture, scaled up with sharp pixels, and passes the keyboard on to the
/// game (see <see cref="KeyMap"/>).
/// </summary>
public sealed class GameWindow : Window
{
    private readonly FrameBuffer _frames;
    private readonly InputQueue _input;
    private readonly Image _image = new() { Stretch = Stretch.Uniform };
    private readonly HashSet<Key> _heldKeys = [];
    private WriteableBitmap? _bitmap;
    private long _shownVersion = -1;

    public GameWindow(FrameBuffer frames, InputQueue input)
    {
        _frames = frames;
        _input = input;
        Title = "C64Bot";
        Width = 800;
        Height = 600;
        Background = Brushes.Black;

        // Scaling up a C64 picture should give bigger square pixels, not a blur.
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.NearestNeighbor);
        Content = _image;

        CompositionTarget.Rendering += OnRendering;
        Closed += (_, _) => CompositionTarget.Rendering -= OnRendering;

        // A key released while another window has the focus never reaches this one, so let go of everything.
        Deactivated += (_, _) => ReleaseHeldKeys();
    }

    // The Preview events come before WPF uses keys like Tab and the arrows to move the focus around.
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (KeyMap.Find(e.Key) is not { } input)
            return;

        e.Handled = true;

        // Windows repeats a held key; the C64 only needs to hear about it once.
        if (_heldKeys.Add(e.Key))
            _input.Add(input, down: true);
    }

    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        if (KeyMap.Find(e.Key) is not { } input)
            return;

        e.Handled = true;
        if (_heldKeys.Remove(e.Key))
            _input.Add(input, down: false);
    }

    private void ReleaseHeldKeys()
    {
        foreach (var key in _heldKeys)
            _input.Add(KeyMap.Find(key)!.Value, down: false);

        _heldKeys.Clear();
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
