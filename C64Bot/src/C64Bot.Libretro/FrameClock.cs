using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace C64Bot.Libretro;

/// <summary>
/// A clock that ticks at a steady frame rate, for running an emulator at the speed of the real machine.
/// </summary>
/// <remarks>
/// Windows' normal timers tick only every 15.6 ms, far too coarse for 50 frames a second. This sleeps on a
/// high-resolution timer (Windows 10 version 1803 or later) until shortly before a frame is due, then spins
/// for the last moment.
/// </remarks>
public sealed partial class FrameClock : IDisposable
{
    private const uint CreateWaitableTimerHighResolution = 0x00000002;
    private const uint TimerAllAccess = 0x001F0003;
    private const uint Infinite = 0xFFFFFFFF;

    private static readonly TimeSpan SpinTime = TimeSpan.FromMilliseconds(1);

    // If the clock falls this far behind (the PC was busy, or a debugger stopped the program), it starts
    // counting again from now instead of racing to catch up.
    private static readonly TimeSpan MaxLag = TimeSpan.FromMilliseconds(100);

    private readonly nint _timer;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly double _secondsPerFrame;
    private TimeSpan _start;
    private long _frame;

    /// <exception cref="Win32Exception">Windows couldn't create a high-resolution timer.</exception>
    public FrameClock(double framesPerSecond)
    {
        _secondsPerFrame = 1 / framesPerSecond;
        _timer = CreateWaitableTimerExW(0, 0, CreateWaitableTimerHighResolution, TimerAllAccess);
        if (_timer == 0)
            throw new Win32Exception(Marshal.GetLastPInvokeError());
    }

    /// <summary>
    /// Waits until the next frame is due. Frames are due at fixed times counted from when the clock was
    /// created, so a late frame doesn't push the later ones back.
    /// </summary>
    public void WaitForNextFrame()
    {
        _frame++;
        var due = _start + TimeSpan.FromSeconds(_frame * _secondsPerFrame);
        var now = _stopwatch.Elapsed;

        if (now - due > MaxLag)
        {
            _start = now - TimeSpan.FromSeconds(_frame * _secondsPerFrame);
            return;
        }

        var sleep = due - now - SpinTime;
        if (sleep > TimeSpan.Zero)
        {
            // A negative due time means "from now", in units of 100 ns, the same unit as TimeSpan ticks.
            var dueTime = -sleep.Ticks;
            SetWaitableTimer(_timer, in dueTime, 0, 0, 0, false);
            WaitForSingleObject(_timer, Infinite);
        }

        while (_stopwatch.Elapsed < due)
            Thread.SpinWait(20);
    }

    public void Dispose() => CloseHandle(_timer);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial nint CreateWaitableTimerExW(nint timerAttributes, nint timerName, uint flags, uint desiredAccess);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWaitableTimer(
        nint timer, in long dueTime, int period, nint completionRoutine, nint completionArgument,
        [MarshalAs(UnmanagedType.Bool)] bool resume);

    [LibraryImport("kernel32.dll")]
    private static partial uint WaitForSingleObject(nint handle, uint milliseconds);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);
}
