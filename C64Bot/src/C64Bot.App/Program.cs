using System.Diagnostics;
using C64Bot.Libretro;

const string CorePath = @"C:\Tools\C64\libretro\vice_x64sc_libretro.dll";
const string DiskPath = @"C:\Emulation\ROMs\C64\Lode Runner.d64";

var coreDirectory = Path.GetDirectoryName(CorePath)!;
var systemDirectory = Directory.CreateDirectory(Path.Combine(coreDirectory, "system")).FullName;
var saveDirectory = Directory.CreateDirectory(Path.Combine(coreDirectory, "saves")).FullName;

using var core = LibretroCore.Load(CorePath);

// The core's log lines are only format strings without their values (see OnLog), so leave them out.
core.Log = message =>
{
    if (!message.StartsWith("Core log", StringComparison.Ordinal))
        Console.WriteLine(message);
};

core.Initialize(systemDirectory, saveDirectory);
core.LoadGame(DiskPath);
Console.WriteLine($"Lode Runner loaded: {core.FramesPerSecond:F3} frames a second.");

RunUntil("The trainer menu asks about unlimited lives", () => ScreenText().Contains("UNLIMITED LIVES"), 3000);
Type(RetroKey.Y);
RunUntil("It asks about invincibility", () => ScreenText().Contains("INVINCIBILITY"), 250);
Type(RetroKey.Y);
RunUntil("It explains the level skip", () => ScreenText().Contains("SKIPS"), 250);
PrintTextScreen();
Type(RetroKey.Space);
RunUntil("The game's own interrupt routine runs", () => InterruptRoutine() == 0x648A, 3000);
Console.WriteLine($"Interrupt routine at ${InterruptRoutine():X4}.");

// Let the level get going first: while it's being set up, the game stops its interrupt routine for a
// moment, and the jiffy clock stands still.
for (var i = 0; i < 250; i++)
    core.RunFrame();

// T5: real time. One frame whenever the frame clock says one is due, for 10 seconds.
const int Seconds = 10;
Console.WriteLine($"Running in real time for {Seconds} seconds ...");

var frameIntervals = new List<double>();
var runTimes = new List<double>();
var process = Process.GetCurrentProcess();
var cpuBefore = process.TotalProcessorTime;
var firstJiffies = JiffyClock();
var stopwatch = Stopwatch.StartNew();
var previousStart = TimeSpan.Zero;

using (var clock = new FrameClock(core.FramesPerSecond))
{
    var frames = (int)(Seconds * core.FramesPerSecond);
    for (var i = 0; i < frames; i++)
    {
        clock.WaitForNextFrame();

        var start = stopwatch.Elapsed;
        if (i > 0)
            frameIntervals.Add((start - previousStart).TotalMilliseconds);
        previousStart = start;

        core.RunFrame();
        runTimes.Add((stopwatch.Elapsed - start).TotalMilliseconds);
    }
}

var real = stopwatch.Elapsed.TotalSeconds;
var emulated = (JiffyClock() - firstJiffies) / 60.0;
process.Refresh();
var cpu = (process.TotalProcessorTime - cpuBefore).TotalSeconds;

Console.WriteLine($"Emulated time: {emulated:F2} s in {real:F2} s of real time ({emulated / real:P1} speed)");
Console.WriteLine($"Time between frames: average {frameIntervals.Average():F3} ms, shortest {frameIntervals.Min():F3} ms, longest {frameIntervals.Max():F3} ms");
Console.WriteLine($"Emulating a frame: average {runTimes.Average():F2} ms, longest {runTimes.Max():F2} ms");
Console.WriteLine($"CPU used: {cpu / real:P0} of one core");

// Runs frames until the condition holds, and says how many it took.
void RunUntil(string description, Func<bool> condition, int maxFrames)
{
    for (var frame = 1; frame <= maxFrames; frame++)
    {
        core.RunFrame();
        if (condition())
        {
            Console.WriteLine($"{description}: after {frame} frames.");
            return;
        }
    }

    Console.WriteLine($"{description}: NOT seen within {maxFrames} frames. The screen:");
    PrintTextScreen();
}

// A quick tap: down for a few frames, then up for a few. The keyboard is scanned about once per frame,
// so a press has to last at least that long to be seen.
void Type(RetroKey key)
{
    core.PressKey(key);
    for (var i = 0; i < 5; i++)
        core.RunFrame();

    core.ReleaseKey(key);
    for (var i = 0; i < 5; i++)
        core.RunFrame();
}

// The interrupt vector at $0314-$0315, low byte first.
int InterruptRoutine()
{
    var ram = core.SystemRam;
    return ram[0x0314] | ram[0x0315] << 8;
}

// The KERNAL's jiffy clock at $A0-$A2, high byte first, counted up 60 times per emulated second.
int JiffyClock()
{
    var ram = core.SystemRam;
    return ram[0xA0] << 16 | ram[0xA1] << 8 | ram[0xA2];
}

// The 40x25 text screen at $0400 as one string, row after row.
string ScreenText()
{
    var screen = core.SystemRam[0x0400..0x07E8];
    var text = new char[screen.Length];
    for (var i = 0; i < screen.Length; i++)
        text[i] = ScreenCodeToChar(screen[i]);

    return new string(text);
}

void PrintTextScreen()
{
    var text = ScreenText();
    for (var row = 0; row < 25; row++)
    {
        var line = text.Substring(row * 40, 40).TrimEnd();
        if (line.Length > 0)
            Console.WriteLine($"  {row,2}: {line}");
    }
}

// Screen codes 1-26 are A-Z and 32-63 match ASCII; bit 7 only means the character is shown reversed.
static char ScreenCodeToChar(byte code)
{
    var c = code & 0x7F;
    return c switch { < 32 => (char)('@' + c), < 64 => (char)c, _ => '#' };
}
