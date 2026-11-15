using System.Runtime.CompilerServices;
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
/// A core is one set of global C functions, so a process can run only one core at a time.
/// </para>
/// </remarks>
public sealed unsafe class LibretroCore : IDisposable
{
    // libretro.h: RETRO_MEMORY_SYSTEM_RAM.
    private const uint MemorySystemRam = 2;

    // libretro.h: RETRO_PIXEL_FORMAT_XRGB8888, 4 bytes per pixel.
    private const int PixelFormatXrgb8888 = 1;

    // libretro.h: the RETRO_ENVIRONMENT_* questions this frontend answers.
    private const uint EnvironmentGetSystemDirectory = 9;
    private const uint EnvironmentSetPixelFormat = 10;
    private const uint EnvironmentSetKeyboardCallback = 12;
    private const uint EnvironmentGetVariable = 15;
    private const uint EnvironmentSetVariables = 16;
    private const uint EnvironmentGetLogInterface = 27;
    private const uint EnvironmentGetSaveDirectory = 31;

    // The callbacks are static C functions, so they find the running core here.
    private static LibretroCore? s_current;

    private readonly nint _library;
    private readonly HashSet<uint> _unansweredCommands = [];

    // Core options by key, with their current values as UTF-8 strings the core can read.
    private readonly Dictionary<string, nint> _options = [];
    private nint _systemDirectory;
    private nint _saveDirectory;
    private nint _gamePath;
    private bool _initialized;
    private bool _gameLoaded;

    // The core's function for key presses, which it hands over with SET_KEYBOARD_CALLBACK.
    private delegate* unmanaged[Cdecl]<byte, uint, uint, ushort, void> _keyboardEvent;

    private LibretroCore(nint library) => _library = library;

    /// <summary>
    /// Loads the core from its DLL file.
    /// </summary>
    /// <exception cref="DllNotFoundException">The file doesn't exist or isn't a DLL for this platform.</exception>
    public static LibretroCore Load(string path) => new(NativeLibrary.Load(path));

    /// <summary>
    /// The version of the libretro interface the core was built for. It's 1 for every current core.
    /// </summary>
    public uint ApiVersion => ((delegate* unmanaged[Cdecl]<uint>)Export("retro_api_version"))();

    /// <summary>
    /// Receives the core's log lines, and a line for every environment command the core sends that this
    /// frontend doesn't answer.
    /// </summary>
    public Action<string>? Log { get; set; }

    /// <summary>
    /// The environment commands the core has sent that this frontend doesn't answer. Environment commands are
    /// the core's questions and requests to the frontend, numbered as in <c>libretro.h</c>.
    /// </summary>
    public IReadOnlyCollection<uint> UnansweredEnvironmentCommands => _unansweredCommands;

    /// <summary>
    /// The core's options (its settings) and their current values. Until changed, each has the default the
    /// core announced.
    /// </summary>
    public IReadOnlyDictionary<string, string> Options =>
        _options.ToDictionary(option => option.Key, option => Marshal.PtrToStringUTF8(option.Value) ?? "");

    /// <summary>
    /// Receives every finished picture, on the thread that calls <see cref="RunFrame"/>.
    /// </summary>
    public VideoFrameHandler? VideoFrame { get; set; }

    /// <summary>
    /// The number of pictures the core has finished, usually one per frame.
    /// </summary>
    public int VideoFrames { get; private set; }

    public int FrameWidth { get; private set; }
    public int FrameHeight { get; private set; }

    /// <summary>
    /// The C64's RAM, read straight from the core's memory. Only valid until the core is disposed.
    /// </summary>
    public ReadOnlySpan<byte> SystemRam
    {
        get
        {
            var data = ((delegate* unmanaged[Cdecl]<uint, void*>)Export("retro_get_memory_data"))(MemorySystemRam);
            var size = ((delegate* unmanaged[Cdecl]<uint, nuint>)Export("retro_get_memory_size"))(MemorySystemRam);
            return new ReadOnlySpan<byte>(data, (int)size);
        }
    }

    /// <summary>
    /// Asks the core for its name, version and the files it can load. Can be called before the core is set up.
    /// </summary>
    public SystemInfo GetSystemInfo()
    {
        RetroSystemInfo info;
        ((delegate* unmanaged[Cdecl]<RetroSystemInfo*, void>)Export("retro_get_system_info"))(&info);

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
    /// <exception cref="InvalidOperationException">Another core is already running in this process.</exception>
    /// <exception cref="ArgumentException">The core has no option with one of the keys.</exception>
    public void Initialize(
        string systemDirectory, string saveDirectory, IReadOnlyDictionary<string, string>? options = null)
    {
        if (s_current is not null)
            throw new InvalidOperationException("Only one libretro core can run in a process at a time.");

        s_current = this;
        _systemDirectory = Marshal.StringToCoTaskMemUTF8(systemDirectory);
        _saveDirectory = Marshal.StringToCoTaskMemUTF8(saveDirectory);

        ((delegate* unmanaged[Cdecl]<delegate* unmanaged[Cdecl]<uint, void*, byte>, void>)
            Export("retro_set_environment"))(&OnEnvironment);
        ((delegate* unmanaged[Cdecl]<delegate* unmanaged[Cdecl]<void*, uint, uint, nuint, void>, void>)
            Export("retro_set_video_refresh"))(&OnVideoRefresh);
        ((delegate* unmanaged[Cdecl]<delegate* unmanaged[Cdecl]<short, short, void>, void>)
            Export("retro_set_audio_sample"))(&OnAudioSample);
        ((delegate* unmanaged[Cdecl]<delegate* unmanaged[Cdecl]<short*, nuint, nuint>, void>)
            Export("retro_set_audio_sample_batch"))(&OnAudioSampleBatch);
        ((delegate* unmanaged[Cdecl]<delegate* unmanaged[Cdecl]<void>, void>)
            Export("retro_set_input_poll"))(&OnInputPoll);
        ((delegate* unmanaged[Cdecl]<delegate* unmanaged[Cdecl]<uint, uint, uint, uint, short>, void>)
            Export("retro_set_input_state"))(&OnInputState);

        // The core announced its options in retro_set_environment and reads them in retro_init.
        foreach (var (key, value) in options ?? new Dictionary<string, string>())
            SetOption(key, value);

        ((delegate* unmanaged[Cdecl]<void>)Export("retro_init"))();
        _initialized = true;
    }

    /// <summary>
    /// Loads a game, for example a disk image, and starts it.
    /// </summary>
    /// <exception cref="InvalidOperationException">The core couldn't load the file.</exception>
    public void LoadGame(string path)
    {
        _gamePath = Marshal.StringToCoTaskMemUTF8(path);
        var info = new RetroGameInfo { Path = _gamePath };

        if (((delegate* unmanaged[Cdecl]<RetroGameInfo*, byte>)Export("retro_load_game"))(&info) == 0)
            throw new InvalidOperationException($"The core couldn't load {path}.");

        _gameLoaded = true;

        // Every frontend asks for the timing after loading a game, and the core sets up its frame rate here.
        RetroSystemAvInfo avInfo;
        ((delegate* unmanaged[Cdecl]<RetroSystemAvInfo*, void>)Export("retro_get_system_av_info"))(&avInfo);
        FramesPerSecond = avInfo.Fps;
        SampleRate = avInfo.SampleRate;
    }

    /// <summary>
    /// How many frames the emulated machine shows per second, as the core reported after loading the game.
    /// </summary>
    public double FramesPerSecond { get; private set; }

    /// <summary>
    /// How many sound samples per second the core sends, as it reported after loading the game.
    /// </summary>
    public double SampleRate { get; private set; }

    /// <summary>
    /// Runs the emulation for one video frame: 1/50 of a second on a PAL C64.
    /// </summary>
    public void RunFrame() => ((delegate* unmanaged[Cdecl]<void>)Export("retro_run"))();

    /// <summary>
    /// Presses a key on the emulated keyboard and holds it down until <see cref="ReleaseKey"/>. The core sees it
    /// at the next frame.
    /// </summary>
    /// <exception cref="InvalidOperationException">The core hasn't asked for key presses.</exception>
    public void PressKey(RetroKey key) => SendKeyboardEvent(key, down: true);

    /// <summary>
    /// Lets go of a key pressed with <see cref="PressKey"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The core hasn't asked for key presses.</exception>
    public void ReleaseKey(RetroKey key) => SendKeyboardEvent(key, down: false);

    public void Dispose()
    {
        if (_gameLoaded)
            ((delegate* unmanaged[Cdecl]<void>)Export("retro_unload_game"))();

        if (_initialized)
            ((delegate* unmanaged[Cdecl]<void>)Export("retro_deinit"))();

        if (s_current == this)
            s_current = null;

        Marshal.FreeCoTaskMem(_systemDirectory);
        Marshal.FreeCoTaskMem(_saveDirectory);
        Marshal.FreeCoTaskMem(_gamePath);
        foreach (var value in _options.Values)
            Marshal.FreeCoTaskMem(value);
        NativeLibrary.Free(_library);
    }

    private nint Export(string name) => NativeLibrary.GetExport(_library, name);

    // The core lists its options as key/value pairs, ending with a null key. Each value reads
    // "Description; default|other|other", so the default is the first choice after "; ".
    private void StoreOptionDefaults(RetroVariable* variables)
    {
        for (var variable = variables; variable->Key != 0; variable++)
        {
            var key = Marshal.PtrToStringUTF8(variable->Key) ?? "";
            var value = Marshal.PtrToStringUTF8(variable->Value) ?? "";
            var choices = value[(value.IndexOf("; ", StringComparison.Ordinal) + 2)..];
            var defaultChoice = choices.Split('|')[0];

            if (_options.Remove(key, out var old))
                Marshal.FreeCoTaskMem(old);
            _options[key] = Marshal.StringToCoTaskMemUTF8(defaultChoice);
        }
    }

    private void SetOption(string key, string value)
    {
        if (!_options.Remove(key, out var old))
            throw new ArgumentException($"The core has no option called {key}.", nameof(key));

        Marshal.FreeCoTaskMem(old);
        _options[key] = Marshal.StringToCoTaskMemUTF8(value);
    }

    private void SendKeyboardEvent(RetroKey key, bool down)
    {
        if (_keyboardEvent == null)
            throw new InvalidOperationException("The core hasn't asked for key presses (SET_KEYBOARD_CALLBACK).");

        // The character is for typing text and the modifiers are Shift, Ctrl and so on; neither is needed here.
        _keyboardEvent(down ? (byte)1 : (byte)0, (uint)key, 0, 0);
    }

    // The callbacks below are called from C. An exception must never escape them, or the process ends.

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static byte OnEnvironment(uint command, void* data)
    {
        var core = s_current!;
        switch (command)
        {
            case EnvironmentGetSystemDirectory:
                *(nint*)data = core._systemDirectory;
                return 1;
            case EnvironmentGetSaveDirectory:
                *(nint*)data = core._saveDirectory;
                return 1;
            case EnvironmentSetPixelFormat:
                // Only XRGB8888 is accepted, the format VideoFrameHandler promises.
                return *(int*)data == PixelFormatXrgb8888 ? (byte)1 : (byte)0;
            case EnvironmentGetLogInterface:
                // struct retro_log_callback holds just the function pointer.
                *(delegate* unmanaged[Cdecl]<int, nint, void>*)data = &OnLog;
                return 1;
            case EnvironmentSetKeyboardCallback:
                // struct retro_keyboard_callback holds just the function pointer.
                core._keyboardEvent = *(delegate* unmanaged[Cdecl]<byte, uint, uint, ushort, void>*)data;
                return 1;
            case EnvironmentSetVariables:
                core.StoreOptionDefaults((RetroVariable*)data);
                return 1;
            case EnvironmentGetVariable:
                var variable = (RetroVariable*)data;
                var key = Marshal.PtrToStringUTF8(variable->Key) ?? "";
                variable->Value = core._options.GetValueOrDefault(key);
                return variable->Value != 0 ? (byte)1 : (byte)0;
            default:
                if (core._unansweredCommands.Add(command))
                    core.Log?.Invoke($"Not answered: environment command {command}");
                return 0;
        }
    }

    // The C function is printf-like: a level, a format string and its arguments. On 64-bit Windows the extra
    // arguments can safely be ignored, so this passes on the format string as it is, placeholders included.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnLog(int level, nint format) =>
        s_current?.Log?.Invoke($"Core log ({level}): {Marshal.PtrToStringUTF8(format)?.TrimEnd()}");

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnVideoRefresh(void* data, uint width, uint height, nuint pitch)
    {
        var core = s_current!;
        core.VideoFrames++;
        core.FrameWidth = (int)width;
        core.FrameHeight = (int)height;

        // data is null when the picture is the same as the last one.
        if (data == null || core.VideoFrame is not { } handler)
            return;

        try
        {
            var length = (int)pitch * ((int)height - 1) + (int)width * 4;
            handler(new ReadOnlySpan<byte>(data, length), (int)width, (int)height, (int)pitch);
        }
        catch (Exception exception)
        {
            core.Log?.Invoke($"The video frame handler failed: {exception.Message}");
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnAudioSample(short left, short right)
    {
    }

    // Must report every sample frame as used, or the core may try to send them again.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static nuint OnAudioSampleBatch(short* data, nuint frames) => frames;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnInputPoll()
    {
    }

    // Nothing is pressed.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static short OnInputState(uint port, uint device, uint index, uint id) => 0;

    // struct retro_system_info in libretro.h. The C bools are single bytes; byte keeps the struct blittable.
    [StructLayout(LayoutKind.Sequential)]
    private struct RetroSystemInfo
    {
        public nint LibraryName;
        public nint LibraryVersion;
        public nint ValidExtensions;
        public byte NeedFullPath;
        public byte BlockExtract;
    }

    // struct retro_variable in libretro.h.
    [StructLayout(LayoutKind.Sequential)]
    private struct RetroVariable
    {
        public nint Key;
        public nint Value;
    }

    // struct retro_system_av_info in libretro.h: the geometry, then the timing.
    [StructLayout(LayoutKind.Sequential)]
    private struct RetroSystemAvInfo
    {
        public uint BaseWidth;
        public uint BaseHeight;
        public uint MaxWidth;
        public uint MaxHeight;
        public float AspectRatio;
        public double Fps;
        public double SampleRate;
    }

    // struct retro_game_info in libretro.h. Only the path is used, because this core loads files itself.
    [StructLayout(LayoutKind.Sequential)]
    private struct RetroGameInfo
    {
        public nint Path;
        public nint Data;
        public nuint Size;
        public nint Meta;
    }
}
