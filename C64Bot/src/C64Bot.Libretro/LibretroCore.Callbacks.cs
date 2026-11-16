using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace C64Bot.Libretro;

// The functions the core calls back into: the frontend's half of the libretro interface.
//
// The core calls them from C, and an exception that got back into C would end the process. So each callback
// that does any work hands it to the running core inside a try/catch; the others can't fail.
public sealed unsafe partial class LibretroCore
{
    // libretro.h: RETRO_PIXEL_FORMAT_XRGB8888, 4 bytes per pixel.
    private const int PixelFormatXrgb8888 = 1;

    // libretro.h: the RETRO_ENVIRONMENT_* commands this frontend answers.
    private const uint EnvironmentGetSystemDirectory = 9;
    private const uint EnvironmentSetPixelFormat = 10;
    private const uint EnvironmentSetKeyboardCallback = 12;
    private const uint EnvironmentGetVariable = 15;
    private const uint EnvironmentSetVariables = 16;
    private const uint EnvironmentGetLogInterface = 27;
    private const uint EnvironmentGetSaveDirectory = 31;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static byte OnEnvironment(uint command, void* data)
    {
        try
        {
            return s_current is { } core && core.HandleEnvironment(command, data) ? (byte)1 : (byte)0;
        }
        catch (Exception exception)
        {
            ReportCallbackFailure(nameof(OnEnvironment), exception);
            return 0;
        }
    }

    // The C function is printf-like: a level, a format string and its arguments. On 64-bit Windows the extra
    // arguments can safely be ignored, so this passes on the format string as it is, placeholders included.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnLog(int level, nint format)
    {
        try
        {
            s_current?.Log?.Invoke($"Core log ({level}): {Marshal.PtrToStringUTF8(format)?.TrimEnd()}");
        }
        catch (Exception exception)
        {
            ReportCallbackFailure(nameof(OnLog), exception);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnVideoRefresh(void* data, uint width, uint height, nuint pitch)
    {
        try
        {
            s_current?.HandleVideoRefresh(data, width, height, pitch);
        }
        catch (Exception exception)
        {
            ReportCallbackFailure(nameof(OnVideoRefresh), exception);
        }
    }

    // There's no sound output yet, so the samples are dropped.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnAudioSample(short left, short right)
    {
    }

    // Must report every sample frame as used, or the core may try to send them again.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static nuint OnAudioSampleBatch(short* data, nuint frames) => frames;

    // The buttons are kept up to date by PressButton and ReleaseButton, so there's nothing to poll.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnInputPoll()
    {
    }

    // The core asks about one button at a time. Only the first RetroPad has buttons; everything else is
    // never pressed. In the first frame after a load, nothing is (see LoadState).
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static short OnInputState(uint port, uint device, uint index, uint id) =>
        s_current is { } core && !core._noInputNextFrame && port == 0 && device == DeviceJoypad && id < 16
            ? (short)((core._pressedButtons >> (int)id) & 1)
            : (short)0;

    // Reports a callback that went wrong through the log. Nothing may escape from here, not even an exception
    // thrown by the log itself.
    private static void ReportCallbackFailure(string callback, Exception exception)
    {
        try
        {
            s_current?.Log?.Invoke($"{callback} failed: {exception.GetType().Name}: {exception.Message}");
        }
        catch
        {
            // Nothing more can be done without letting the exception reach the core.
        }
    }

    private bool HandleEnvironment(uint command, void* data)
    {
        switch (command)
        {
            case EnvironmentGetSystemDirectory:
                *(nint*)data = _systemDirectory;
                return true;

            case EnvironmentSetPixelFormat:
                // Only XRGB8888 is accepted, the format VideoFrameHandler promises.
                return *(int*)data == PixelFormatXrgb8888;

            case EnvironmentSetKeyboardCallback:
                // struct retro_keyboard_callback holds just the function pointer.
                _keyboardEvent = *(delegate* unmanaged[Cdecl]<byte, uint, uint, ushort, void>*)data;
                return true;

            case EnvironmentGetVariable:
                var variable = (RetroVariable*)data;
                variable->Value = _options.GetValueOrDefault(Marshal.PtrToStringUTF8(variable->Key) ?? "");
                return variable->Value != 0;

            case EnvironmentSetVariables:
                StoreOptionDefaults((RetroVariable*)data);
                return true;

            case EnvironmentGetLogInterface:
                // struct retro_log_callback holds just the function pointer.
                *(delegate* unmanaged[Cdecl]<int, nint, void>*)data = &OnLog;
                return true;

            case EnvironmentGetSaveDirectory:
                *(nint*)data = _saveDirectory;
                return true;

            default:
                if (_unansweredCommands.Add(command))
                    Log?.Invoke($"Not answered: environment command {command}");
                return false;
        }
    }

    private void HandleVideoRefresh(void* data, uint width, uint height, nuint pitch)
    {
        VideoFrames++;
        FrameWidth = (int)width;
        FrameHeight = (int)height;

        // data is null when the picture is the same as the last one.
        if (data == null || VideoFrame is not { } handler)
            return;

        var length = (int)pitch * ((int)height - 1) + (int)width * 4;
        handler(new ReadOnlySpan<byte>(data, length), (int)width, (int)height, (int)pitch);
    }
}
