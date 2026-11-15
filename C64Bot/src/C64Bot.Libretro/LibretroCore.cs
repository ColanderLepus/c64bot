using System.Runtime.InteropServices;

namespace C64Bot.Libretro;

/// <summary>
/// A libretro core: an emulator built as a DLL, which a host program (the frontend) loads and runs one
/// frame at a time.
/// </summary>
/// <remarks>
/// <para>
/// The interface is plain C, described in <c>libretro.h</c>
/// (https://github.com/libretro/RetroArch/blob/master/libretro-common/include/libretro.h).
/// </para>
/// <para>
/// The steps come in a fixed order: <see cref="Load"/>, <see cref="Initialize"/> and <see cref="LoadGame"/>,
/// then <see cref="RunFrame"/> once per frame. A core is one set of global C functions, so a process can run
/// only one core at a time.
/// </para>
/// <para>
/// Not thread-safe: use a core from one thread only. The core calls back into this class on that same thread,
/// from inside the calls made to it.
/// </para>
/// </remarks>
public sealed unsafe partial class LibretroCore : IDisposable
{
    // libretro.h: RETRO_MEMORY_SYSTEM_RAM.
    private const uint MemorySystemRam = 2;

    // libretro.h: RETRO_DEVICE_JOYPAD, the RetroPad.
    private const uint DeviceJoypad = 1;

    // The callbacks are static C functions, so they find the running core here.
    private static LibretroCore? s_current;

    private readonly nint _library;
    private readonly CoreFunctions _functions;
    private readonly HashSet<uint> _unansweredCommands = [];

    // Core options by key, with their current values as UTF-8 strings the core can read.
    private readonly Dictionary<string, nint> _options = [];

    private nint _systemDirectory;
    private nint _saveDirectory;
    private nint _gamePath;
    private bool _initialized;
    private bool _gameLoaded;
    private bool _disposed;

    // The core's function for key presses, which it hands over with SET_KEYBOARD_CALLBACK.
    private delegate* unmanaged[Cdecl]<byte, uint, uint, ushort, void> _keyboardEvent;

    // The buttons held down on the first RetroPad, one bit per button, numbered as in RetroPadButton.
    private int _pressedButtons;

    private LibretroCore(nint library, CoreFunctions functions)
    {
        _library = library;
        _functions = functions;
    }

    /// <summary>
    /// Loads the core from its DLL file.
    /// </summary>
    /// <exception cref="DllNotFoundException">The file doesn't exist or isn't a DLL for this platform.</exception>
    /// <exception cref="EntryPointNotFoundException">
    /// The DLL isn't a libretro core: a function every core has is missing.
    /// </exception>
    public static LibretroCore Load(string path)
    {
        var library = NativeLibrary.Load(path);
        try
        {
            return new LibretroCore(library, new CoreFunctions(library));
        }
        catch
        {
            NativeLibrary.Free(library);
            throw;
        }
    }

    /// <summary>
    /// Receives the core's log lines, and a line for every environment command the core sends that this
    /// frontend doesn't answer.
    /// </summary>
    public Action<string>? Log { get; set; }

    /// <summary>
    /// Receives every finished picture, on the thread that calls <see cref="RunFrame"/>.
    /// </summary>
    public VideoFrameHandler? VideoFrame { get; set; }

    /// <summary>
    /// The version of the libretro interface the core was built for. It's 1 for every current core.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The core has been disposed.</exception>
    public uint ApiVersion
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _functions.ApiVersion();
        }
    }

    /// <summary>
    /// The core's options (its settings) and their current values. Until changed, each has the default the
    /// core announced.
    /// </summary>
    public IReadOnlyDictionary<string, string> Options =>
        _options.ToDictionary(option => option.Key, option => Marshal.PtrToStringUTF8(option.Value) ?? "");

    /// <summary>
    /// The environment commands the core has sent that this frontend doesn't answer. Environment commands are
    /// the core's questions and requests to the frontend, numbered as in <c>libretro.h</c>.
    /// </summary>
    public IReadOnlyCollection<uint> UnansweredEnvironmentCommands => _unansweredCommands;

    /// <summary>
    /// How many frames the emulated machine shows per second, as the core reported after loading the game.
    /// </summary>
    public double FramesPerSecond { get; private set; }

    /// <summary>
    /// How many sound samples per second the core sends, as it reported after loading the game.
    /// </summary>
    public double SampleRate { get; private set; }

    /// <summary>
    /// The number of pictures the core has finished, usually one per frame.
    /// </summary>
    public int VideoFrames { get; private set; }

    /// <summary>
    /// The width of the last picture the core finished, in pixels.
    /// </summary>
    public int FrameWidth { get; private set; }

    /// <summary>
    /// The height of the last picture the core finished, in pixels.
    /// </summary>
    public int FrameHeight { get; private set; }

    /// <summary>
    /// The emulated machine's RAM, read straight from the core's memory. For the VICE core: the C64's 64 KB.
    /// </summary>
    /// <remarks>
    /// Plain RAM only. Where the machine shows ROM or I/O chips instead, for example the C64's joystick ports
    /// at <c>$DC00</c>, this holds the RAM underneath. Only valid until the core is disposed.
    /// </remarks>
    /// <exception cref="InvalidOperationException">No game has been loaded.</exception>
    /// <exception cref="ObjectDisposedException">The core has been disposed.</exception>
    public ReadOnlySpan<byte> SystemRam
    {
        get
        {
            ThrowUnlessGameLoaded();
            var data = _functions.GetMemoryData(MemorySystemRam);
            var size = _functions.GetMemorySize(MemorySystemRam);
            return new ReadOnlySpan<byte>(data, (int)size);
        }
    }

    /// <summary>
    /// Asks the core for its name, version and the files it can load. Can be called before the core is set up.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The core has been disposed.</exception>
    public SystemInfo GetSystemInfo()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        RetroSystemInfo info;
        _functions.GetSystemInfo(&info);

        return new SystemInfo(
            Marshal.PtrToStringUTF8(info.LibraryName) ?? "",
            Marshal.PtrToStringUTF8(info.LibraryVersion) ?? "",
            Marshal.PtrToStringUTF8(info.ValidExtensions) ?? "",
            info.NeedFullPath != 0);
    }

    /// <summary>
    /// Gives the core its callbacks and sets it up. Must come before <see cref="LoadGame"/>.
    /// </summary>
    /// <param name="systemDirectory">A folder where the core can find and keep its own files, such as ROMs.</param>
    /// <param name="saveDirectory">A folder where the core can save files belonging to games.</param>
    /// <param name="options">Core options to use instead of their defaults, by key.</param>
    /// <exception cref="InvalidOperationException">
    /// The core is already set up, or another core is already running in this process.
    /// </exception>
    /// <exception cref="ArgumentException">The core has no option with one of the keys.</exception>
    /// <exception cref="ObjectDisposedException">The core has been disposed.</exception>
    public void Initialize(
        string systemDirectory, string saveDirectory, IReadOnlyDictionary<string, string>? options = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_initialized)
            throw new InvalidOperationException("The core is already set up.");
        if (s_current is not null)
            throw new InvalidOperationException("Only one libretro core can run in a process at a time.");

        s_current = this;
        try
        {
            // The core asks for these folders while it receives its callbacks, so they must exist first. Any left
            // over from an earlier attempt that failed are freed.
            Marshal.FreeCoTaskMem(_systemDirectory);
            Marshal.FreeCoTaskMem(_saveDirectory);
            _systemDirectory = Marshal.StringToCoTaskMemUTF8(systemDirectory);
            _saveDirectory = Marshal.StringToCoTaskMemUTF8(saveDirectory);

            _functions.SetEnvironment(&OnEnvironment);
            _functions.SetVideoRefresh(&OnVideoRefresh);
            _functions.SetAudioSample(&OnAudioSample);
            _functions.SetAudioSampleBatch(&OnAudioSampleBatch);
            _functions.SetInputPoll(&OnInputPoll);
            _functions.SetInputState(&OnInputState);

            // The core announced its options in retro_set_environment and reads them in retro_init.
            foreach (var (key, value) in options ?? new Dictionary<string, string>())
                SetOption(key, value);

            _functions.Init();
            _initialized = true;
        }
        catch
        {
            // A core that failed to start mustn't keep another one from starting.
            s_current = null;
            throw;
        }
    }

    /// <summary>
    /// Loads a game, for example a disk image, and starts it.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The core isn't set up yet, a game is already loaded, or the core couldn't load the file.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The core has been disposed.</exception>
    public void LoadGame(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_initialized)
            throw new InvalidOperationException("Set up the core with Initialize before loading a game.");
        if (_gameLoaded)
            throw new InvalidOperationException("A game is already loaded.");

        Marshal.FreeCoTaskMem(_gamePath);
        _gamePath = Marshal.StringToCoTaskMemUTF8(path);
        var info = new RetroGameInfo { Path = _gamePath };

        if (_functions.LoadGame(&info) == 0)
            throw new InvalidOperationException($"The core couldn't load {path}.");

        _gameLoaded = true;

        // Every frontend asks for the timing after loading a game, and the core sets up its frame rate here.
        RetroSystemAvInfo avInfo;
        _functions.GetSystemAvInfo(&avInfo);
        FramesPerSecond = avInfo.Fps;
        SampleRate = avInfo.SampleRate;

        // Frontends also say what's plugged into each port. Until then the core treats the first port as
        // empty and ignores its buttons.
        _functions.SetControllerPortDevice(0, DeviceJoypad);
    }

    /// <summary>
    /// Runs the emulation for one video frame: 1/<see cref="FramesPerSecond"/> of a second of emulated time,
    /// 1/50.125 of a second on a PAL C64.
    /// </summary>
    /// <exception cref="InvalidOperationException">No game has been loaded.</exception>
    /// <exception cref="ObjectDisposedException">The core has been disposed.</exception>
    public void RunFrame()
    {
        ThrowUnlessGameLoaded();
        _functions.Run();
    }

    /// <summary>
    /// Presses a key on the emulated keyboard and holds it down until <see cref="ReleaseKey"/>. The core sees it
    /// at the next frame.
    /// </summary>
    /// <exception cref="InvalidOperationException">The core hasn't asked for key presses.</exception>
    /// <exception cref="ObjectDisposedException">The core has been disposed.</exception>
    public void PressKey(RetroKey key) => SendKeyboardEvent(key, down: true);

    /// <summary>
    /// Lets go of a key pressed with <see cref="PressKey"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The core hasn't asked for key presses.</exception>
    /// <exception cref="ObjectDisposedException">The core has been disposed.</exception>
    public void ReleaseKey(RetroKey key) => SendKeyboardEvent(key, down: false);

    /// <summary>
    /// Holds down a button on the first RetroPad until <see cref="ReleaseButton"/>. The core sees it at the next
    /// frame.
    /// </summary>
    public void PressButton(RetroPadButton button) => _pressedButtons |= 1 << (int)button;

    /// <summary>
    /// Lets go of a button pressed with <see cref="PressButton"/>.
    /// </summary>
    public void ReleaseButton(RetroPadButton button) => _pressedButtons &= ~(1 << (int)button);

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_gameLoaded)
            _functions.UnloadGame();

        if (_initialized)
            _functions.Deinit();

        if (s_current == this)
            s_current = null;

        Marshal.FreeCoTaskMem(_systemDirectory);
        Marshal.FreeCoTaskMem(_saveDirectory);
        Marshal.FreeCoTaskMem(_gamePath);
        foreach (var value in _options.Values)
            Marshal.FreeCoTaskMem(value);

        _options.Clear();
        _keyboardEvent = null;
        NativeLibrary.Free(_library);
    }

    // A value reads "Description; default|other|other", so the default is the first choice after "; ".
    internal static string ParseDefaultValue(string value)
    {
        var separator = value.IndexOf("; ", StringComparison.Ordinal);
        var choices = separator < 0 ? value : value[(separator + 2)..];
        var end = choices.IndexOf('|');
        return end < 0 ? choices : choices[..end];
    }

    private void ThrowUnlessGameLoaded()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_gameLoaded)
            throw new InvalidOperationException("Load a game with LoadGame first.");
    }

    private void SetOption(string key, string value)
    {
        if (!_options.Remove(key, out var old))
            throw new ArgumentException($"The core has no option called {key}.", nameof(key));

        Marshal.FreeCoTaskMem(old);
        _options[key] = Marshal.StringToCoTaskMemUTF8(value);
    }

    // The core lists its options as key/value pairs, ending with a null key.
    private void StoreOptionDefaults(RetroVariable* variables)
    {
        for (var variable = variables; variable->Key != 0; variable++)
        {
            var key = Marshal.PtrToStringUTF8(variable->Key) ?? "";
            var defaultValue = ParseDefaultValue(Marshal.PtrToStringUTF8(variable->Value) ?? "");

            if (_options.Remove(key, out var old))
                Marshal.FreeCoTaskMem(old);
            _options[key] = Marshal.StringToCoTaskMemUTF8(defaultValue);
        }
    }

    private void SendKeyboardEvent(RetroKey key, bool down)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_keyboardEvent == null)
            throw new InvalidOperationException("The core hasn't asked for key presses (SET_KEYBOARD_CALLBACK).");

        // The character is for typing text and the modifiers are Shift, Ctrl and so on; neither is needed here.
        _keyboardEvent(down ? (byte)1 : (byte)0, (uint)key, 0, 0);
    }
}
