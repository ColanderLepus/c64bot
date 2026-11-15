using System.IO;
using System.Windows;
using C64Bot.App;
using C64Bot.Libretro;
using C64Bot.LodeRunner;

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

core.Initialize(systemDirectory, saveDirectory, new Dictionary<string, string>
{
    ["vice_gfx_colors"] = "24bit", // the pictures come in the format WPF shows directly
    ["vice_joyport"] = "2",        // the first RetroPad is the joystick in port 2, which Lode Runner reads
});

core.LoadGame(DiskPath);
Console.WriteLine($"Lode Runner loaded: {core.FramesPerSecond:F3} frames a second.");

// The trainer menu: unlimited lives yes, invincibility no (with invincibility, the demo gets stuck and
// SPACE no longer starts the game; see lode-runner.md).
RunUntil("The trainer menu asks about unlimited lives", () => ScreenText().Contains("UNLIMITED LIVES"), 3000);
Type(RetroKey.Y);
RunUntil("It asks about invincibility", () => ScreenText().Contains("INVINCIBILITY"), 250);
Type(RetroKey.N);
RunUntil("It explains the level skip", () => ScreenText().Contains("SKIPS"), 250);
PrintTextScreen();
Type(RetroKey.Space);
RunUntil("The game's own interrupt routine runs", () => InterruptRoutine() == 0x648A, 3000);
Console.WriteLine($"Interrupt routine at ${InterruptRoutine():X4}.");

// T6: watch it. The window runs on a thread of its own: WPF needs a single-threaded apartment (STA)
// thread, and this way the emulation loop below never waits for the window.
// T10: the window's keys come back the other way, through a queue.
var frames = new FrameBuffer();
core.VideoFrame = frames.Store;
var input = new InputQueue();

var windowClosed = new ManualResetEventSlim();
var windowThread = new Thread(() =>
{
    var window = new GameWindow(frames, input);
    window.Closed += (_, _) => windowClosed.Set();
    new Application().Run(window);
});
windowThread.SetApartmentState(ApartmentState.STA);
windowThread.Start();

Console.WriteLine("Running in real time. Close the window to stop.");
using (var clock = new FrameClock(core.FramesPerSecond))
{
    // After the trainer menu the game shows its title screen and then plays itself (the demo), until SPACE
    // is pressed. The window's keyboard goes to the game (see KeyMap). The keys in this console window are
    // for the app itself: T saves a state in memory and L loads it, so the game jumps back to that moment.
    // R remembers the RAM and D shows what has changed since then (M2 F1).
    Console.WriteLine("In the window: arrow keys and Right Ctrl are the joystick, other keys the C64's keyboard.");
    Console.WriteLine("SPACE starts the game from the demo.");
    Console.WriteLine("In this console window: T saves a state, L loads it.");
    Console.WriteLine("R remembers the RAM, D shows what has changed since then.");

    byte[]? savedState = null;
    var savedAt = 0;
    RamImage? rememberedRam = null;
    var rememberedAt = 0;

    // M2 F4: say when the game's phase changes, with the bytes the phase is read from, and when the level number
    // changes (it goes up as soon as a level is completed).
    var phaseReader = new GamePhaseReader();
    GamePhase? phase = null;
    int? level = null;

    for (var frame = 1; !windowClosed.IsSet; frame++)
    {
        input.ApplyTo(core);

        while (Console.KeyAvailable)
        {
            var key = Console.ReadKey(intercept: true).Key;
            if (key == ConsoleKey.T)
            {
                savedState = core.SaveState();
                savedAt = frame;
                Console.WriteLine($"Frame {frame}: saved a state ({savedState.Length:N0} bytes).");
            }
            else if (key == ConsoleKey.L && savedState is not null)
            {
                core.LoadState(savedState);
                Console.WriteLine($"Frame {frame}: loaded the state saved at frame {savedAt}.");
            }
            else if (key == ConsoleKey.R)
            {
                rememberedRam = new RamImage(core.SystemRam);
                rememberedAt = frame;
                Console.WriteLine($"Frame {frame}: remembered the RAM.");
            }
            else if (key == ConsoleKey.D && rememberedRam is not null)
            {
                Console.WriteLine($"Frame {frame}: changes since frame {rememberedAt}:");
                PrintRamChanges(rememberedRam.Compare(new RamImage(core.SystemRam)));
            }
        }

        clock.WaitForNextFrame();
        core.RunFrame();

        var newPhase = phaseReader.Update(core.SystemRam);
        if (newPhase != phase)
        {
            phase = newPhase;
            var ram = core.SystemRam;
            Console.WriteLine(
                $"Frame {frame}: {phase} (" +
                $"game mode ${ram[RamMap.GameMode]:X2}, alive ${ram[RamMap.Alive]:X2}, " +
                $"gold left ${ram[RamMap.GoldLeft]:X2}, lives ${ram[RamMap.Lives]:X2}, " +
                $"interrupt counter ${ram[RamMap.InterruptCounter]:X2})");
        }

        if (phase is not (GamePhase.Other or GamePhase.Title or GamePhase.Demo) && core.SystemRam[RamMap.Level] != level)
        {
            level = core.SystemRam[RamMap.Level];
            Console.WriteLine($"Frame {frame}: level {level}");
        }
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

// One line per 256-byte page that changed. A game redraws its screens all the time, which would bury the
// interesting changes, so the bytes themselves are only shown for pages with a few changes.
static void PrintRamChanges(IReadOnlyList<RamChange> changes)
{
    const int MaxDetailedBytes = 16;

    if (changes.Count == 0)
    {
        Console.WriteLine("  none");
        return;
    }

    // A run can cross into the next page, so split each run at the page boundaries first.
    var pieces = changes.SelectMany(change =>
        Enumerable.Range(change.Start >> 8, ((change.End - 1) >> 8) - (change.Start >> 8) + 1).Select(page =>
        {
            var start = Math.Max(change.Start, page << 8);
            var end = Math.Min(change.End, (page + 1) << 8);
            var offset = start - change.Start;
            return new RamChange(start, change.Before[offset..(end - change.Start)], change.After[offset..(end - change.Start)]);
        }));

    foreach (var page in pieces.GroupBy(piece => piece.Start >> 8))
    {
        var count = page.Sum(piece => piece.Before.Length);
        Console.WriteLine($"  page ${page.Key:X2}: {count:N0} bytes");
        if (count > MaxDetailedBytes)
            continue;

        foreach (var piece in page)
        {
            var addresses = piece.Before.Length == 1 ? $"${piece.Start:X4}" : $"${piece.Start:X4}-${piece.End - 1:X4}";
            Console.WriteLine($"    {addresses}: {Hex(piece.Before)} -> {Hex(piece.After)}");
        }
    }

    static string Hex(byte[] bytes) => string.Join(' ', bytes.Select(b => b.ToString("X2")));
}

// Screen codes 1-26 are A-Z and 32-63 match ASCII; bit 7 only means the character is shown reversed.
static char ScreenCodeToChar(byte code)
{
    var c = code & 0x7F;
    return c switch { < 32 => (char)('@' + c), < 64 => (char)c, _ => '#' };
}
