using System.Runtime.InteropServices;

namespace C64Bot.Libretro;

/// <summary>
/// A libretro core: an emulator built as a DLL, which a host program (the frontend) loads and runs one
/// frame at a time.
/// </summary>
/// <remarks>
/// The interface is plain C, described in <c>libretro.h</c>
/// (https://github.com/libretro/RetroArch/blob/master/libretro-common/include/libretro.h).
/// </remarks>
public sealed unsafe class LibretroCore : IDisposable
{
    private readonly nint _library;

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

    public void Dispose() => NativeLibrary.Free(_library);

    private nint Export(string name) => NativeLibrary.GetExport(_library, name);

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
}
