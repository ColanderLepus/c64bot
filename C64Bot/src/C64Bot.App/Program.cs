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

// The level to start at can be given on the command line; level 1 if not.
var level = 1;
if (args.Length > 0 && (!int.TryParse(args[0], out level) || level is < 1 or > GameStarter.MaxLevel))
{
    Console.WriteLine($"Give a level from 1 to {GameStarter.MaxLevel}, not '{args[0]}'.");
    return;
}

core.LoadGame(DiskPath);
Console.WriteLine($"Lode Runner loaded: {core.FramesPerSecond:F3} frames a second.");

// M2 F5: flat out to the start of a new game (unlimited lives yes, invincibility no, D6), then a new game at the
// chosen level.
var startTime = System.Diagnostics.Stopwatch.StartNew();
var starter = GameStarter.Start(core, unlimitedLives: true);
Console.WriteLine($"At the start of a new game after {starter.StartFrames:N0} frames, {startTime.Elapsed.TotalSeconds:F1} s.");
starter.StartLevel(level);
Console.WriteLine($"Starting level {level}.");

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
    // The game sets up the level and starts it by itself; after a death it waits for a move (hold a direction
    // until the level runs). The window's keyboard goes to the game (see KeyMap). The keys in this console window are for the app itself:
    // T saves a state in memory and L loads it, so the game jumps back to that moment. R remembers the RAM and D
    // shows what has changed since then (M2 F1).
    Console.WriteLine("In the window: arrow keys and Right Ctrl are the joystick, other keys the C64's keyboard.");
    Console.WriteLine("The level starts by itself. After a death, hold a direction until it runs again.");
    Console.WriteLine("In this console window: T saves a state, L loads it.");
    Console.WriteLine("R remembers the RAM, D shows what has changed since then. G shows the game state.");

    byte[]? savedState = null;
    var savedAt = 0;
    RamImage? rememberedRam = null;
    var rememberedAt = 0;

    // M2 F4: say when the game's phase changes, with the bytes the phase is read from, and when the level number
    // changes (it goes up as soon as a level is completed).
    var phaseReader = new GamePhaseReader();
    GamePhase? phase = null;
    int? shownLevel = null;

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
            else if (key == ConsoleKey.G)
            {
                Console.WriteLine($"Frame {frame}: the game state (M2 F8):");
                PrintGameState(core.SystemRam);
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

        if (phase is not (GamePhase.Other or GamePhase.Title or GamePhase.Demo) && core.SystemRam[RamMap.LevelNumber] != shownLevel)
        {
            shownLevel = core.SystemRam[RamMap.LevelNumber];
            Console.WriteLine($"Frame {frame}: level {shownLevel}");
        }
    }
}

windowThread.Join();

// The game state as the bot would see it, framed so the level's edges show; then the player's values and the
// hidden exit ladders.
static void PrintGameState(ReadOnlySpan<byte> ram)
{
    if (!GameState.TryRead(ram, out var state))
    {
        Console.WriteLine("  Not readable in this frame (no level in play, or the player's move caught halfway).");
        return;
    }

    var border = "  +" + new string('-', Level.Width) + "+";
    Console.WriteLine(border);
    foreach (var line in state.ToString().TrimEnd('\n').Split('\n'))
        Console.WriteLine($"  |{line}|");
    Console.WriteLine(border);

    var player = state.Player;
    Console.WriteLine(
        $"  Player: ({player.Position.Column},{player.Position.Row}) offset {player.OffsetX:+0;-0;0},{player.OffsetY:+0;-0;0}, " +
        $"{player.Pose} facing {player.Facing?.ToString() ?? "-"}" +
        (player.Falling ? ", falling" : "") +
        (player.Digging is { } side ? $", digging {side}" : ""));

    var ladders = state.Level.HiddenLadders;
    Console.WriteLine("  Hidden exit ladders: " +
        (ladders.Count == 0 ? "none" : string.Join(" ", ladders.Select(p => $"({p.Column},{p.Row})"))));
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
