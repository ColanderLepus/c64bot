using System.Runtime.InteropServices;

namespace C64Bot.Libretro;

// The C side of the libretro interface: the functions every core exports, and the structs that cross between
// the frontend and the core, all as in libretro.h.
public sealed unsafe partial class LibretroCore
{
    // The core's functions, looked up once when the DLL is loaded, so a call costs no more than a normal C call.
    // Every core exports all of them; a missing one means the DLL isn't a libretro core.
    private readonly struct CoreFunctions
    {
        public readonly delegate* unmanaged[Cdecl]<uint> ApiVersion;
        public readonly delegate* unmanaged[Cdecl]<RetroSystemInfo*, void> GetSystemInfo;
        public readonly delegate* unmanaged[Cdecl]<delegate* unmanaged[Cdecl]<uint, void*, byte>, void> SetEnvironment;
        public readonly delegate* unmanaged[Cdecl]<delegate* unmanaged[Cdecl]<void*, uint, uint, nuint, void>, void> SetVideoRefresh;
        public readonly delegate* unmanaged[Cdecl]<delegate* unmanaged[Cdecl]<short, short, void>, void> SetAudioSample;
        public readonly delegate* unmanaged[Cdecl]<delegate* unmanaged[Cdecl]<short*, nuint, nuint>, void> SetAudioSampleBatch;
        public readonly delegate* unmanaged[Cdecl]<delegate* unmanaged[Cdecl]<void>, void> SetInputPoll;
        public readonly delegate* unmanaged[Cdecl]<delegate* unmanaged[Cdecl]<uint, uint, uint, uint, short>, void> SetInputState;
        public readonly delegate* unmanaged[Cdecl]<void> Init;
        public readonly delegate* unmanaged[Cdecl]<void> Deinit;
        public readonly delegate* unmanaged[Cdecl]<RetroGameInfo*, byte> LoadGame;
        public readonly delegate* unmanaged[Cdecl]<void> UnloadGame;
        public readonly delegate* unmanaged[Cdecl]<RetroSystemAvInfo*, void> GetSystemAvInfo;
        public readonly delegate* unmanaged[Cdecl]<uint, uint, void> SetControllerPortDevice;
        public readonly delegate* unmanaged[Cdecl]<void> Run;
        public readonly delegate* unmanaged[Cdecl]<uint, void*> GetMemoryData;
        public readonly delegate* unmanaged[Cdecl]<uint, nuint> GetMemorySize;

        public CoreFunctions(nint library)
        {
            ApiVersion = (delegate* unmanaged[Cdecl]<uint>)Export(library, "retro_api_version");
            GetSystemInfo = (delegate* unmanaged[Cdecl]<RetroSystemInfo*, void>)Export(library, "retro_get_system_info");
            SetEnvironment = (delegate* unmanaged[Cdecl]<delegate* unmanaged[Cdecl]<uint, void*, byte>, void>)
                Export(library, "retro_set_environment");
            SetVideoRefresh = (delegate* unmanaged[Cdecl]<delegate* unmanaged[Cdecl]<void*, uint, uint, nuint, void>, void>)
                Export(library, "retro_set_video_refresh");
            SetAudioSample = (delegate* unmanaged[Cdecl]<delegate* unmanaged[Cdecl]<short, short, void>, void>)
                Export(library, "retro_set_audio_sample");
            SetAudioSampleBatch = (delegate* unmanaged[Cdecl]<delegate* unmanaged[Cdecl]<short*, nuint, nuint>, void>)
                Export(library, "retro_set_audio_sample_batch");
            SetInputPoll = (delegate* unmanaged[Cdecl]<delegate* unmanaged[Cdecl]<void>, void>)
                Export(library, "retro_set_input_poll");
            SetInputState = (delegate* unmanaged[Cdecl]<delegate* unmanaged[Cdecl]<uint, uint, uint, uint, short>, void>)
                Export(library, "retro_set_input_state");
            Init = (delegate* unmanaged[Cdecl]<void>)Export(library, "retro_init");
            Deinit = (delegate* unmanaged[Cdecl]<void>)Export(library, "retro_deinit");
            LoadGame = (delegate* unmanaged[Cdecl]<RetroGameInfo*, byte>)Export(library, "retro_load_game");
            UnloadGame = (delegate* unmanaged[Cdecl]<void>)Export(library, "retro_unload_game");
            GetSystemAvInfo = (delegate* unmanaged[Cdecl]<RetroSystemAvInfo*, void>)Export(library, "retro_get_system_av_info");
            SetControllerPortDevice = (delegate* unmanaged[Cdecl]<uint, uint, void>)
                Export(library, "retro_set_controller_port_device");
            Run = (delegate* unmanaged[Cdecl]<void>)Export(library, "retro_run");
            GetMemoryData = (delegate* unmanaged[Cdecl]<uint, void*>)Export(library, "retro_get_memory_data");
            GetMemorySize = (delegate* unmanaged[Cdecl]<uint, nuint>)Export(library, "retro_get_memory_size");
        }

        private static nint Export(nint library, string name) => NativeLibrary.GetExport(library, name);
    }

    // struct retro_system_info. The C bools are single bytes; byte keeps the struct blittable.
    [StructLayout(LayoutKind.Sequential)]
    private struct RetroSystemInfo
    {
        public nint LibraryName;
        public nint LibraryVersion;
        public nint ValidExtensions;
        public byte NeedFullPath;
        public byte BlockExtract;
    }

    // struct retro_game_info. Only the path is used, because the VICE core loads files itself.
    [StructLayout(LayoutKind.Sequential)]
    private struct RetroGameInfo
    {
        public nint Path;
        public nint Data;
        public nuint Size;
        public nint Meta;
    }

    // struct retro_system_av_info: the geometry, then the timing.
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

    // struct retro_variable: a core option's key and value.
    [StructLayout(LayoutKind.Sequential)]
    private struct RetroVariable
    {
        public nint Key;
        public nint Value;
    }
}
