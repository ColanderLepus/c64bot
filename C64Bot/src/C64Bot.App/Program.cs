using System.IO;
using System.Windows;
using C64Bot.App;
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

// 24-bit colour, so the pictures come in the format WPF shows directly.
core.Initialize(systemDirectory, saveDirectory, new Dictionary<string, string> { ["vice_gfx_colors"] = "24bit" });
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

// T6: watch it. The window runs on a thread of its own: WPF needs a single-threaded apartment (STA)
// thread, and this way the emulation loop below never waits for the window.
var frames = new FrameBuffer();
core.VideoFrame = frames.Store;

var windowClosed = new ManualResetEventSlim();
var windowThread = new Thread(() =>
{
    var window = new GameWindow(frames);
    window.Closed += (_, _) => windowClosed.Set();
    new Application().Run(window);
});
windowThread.SetApartmentState(ApartmentState.STA);
windowThread.Start();

Console.WriteLine("Running in real time. Close the window to stop.");
using (var clock = new FrameClock(core.FramesPerSecond))
{
    while (!windowClosed.IsSet)
    {
        clock.WaitForNextFrame();
        core.RunFrame();
    }
}

windowThread.Join();

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
