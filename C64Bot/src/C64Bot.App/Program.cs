using System.Diagnostics;
using C64Bot.Libretro;

const string CorePath = @"C:\Tools\C64\libretro\vice_x64sc_libretro.dll";
const string DiskPath = @"C:\Emulation\ROMs\C64\Lode Runner.d64";
const int Frames = 3000; // 60 emulated seconds at 50 frames a second

var coreDirectory = Path.GetDirectoryName(CorePath)!;
var systemDirectory = Directory.CreateDirectory(Path.Combine(coreDirectory, "system")).FullName;
var saveDirectory = Directory.CreateDirectory(Path.Combine(coreDirectory, "saves")).FullName;

using var core = LibretroCore.Load(CorePath);
core.Log = Console.WriteLine;
core.Initialize(systemDirectory, saveDirectory);
core.LoadGame(DiskPath);
Console.WriteLine($"Lode Runner loaded: {core.FramesPerSecond:F3} frames a second, sound at {core.SampleRate} Hz.");

var clock = Stopwatch.StartNew();
for (var frame = 1; frame <= Frames; frame++)
{
    core.RunFrame();

    if (frame % 250 == 0)
    {
        var ram = core.SystemRam;
        var interruptRoutine = ram[0x0314] | ram[0x0315] << 8;
        Console.WriteLine($"Frame {frame,4}: interrupt routine at ${interruptRoutine:X4} ({clock.Elapsed.TotalSeconds:F1} s)");
    }
}

Console.WriteLine(
    $"{Frames} frames ({Frames / 50} emulated seconds) in {clock.Elapsed.TotalSeconds:F1} s. " +
    $"The core sent {core.VideoFrames} pictures of {core.FrameWidth}x{core.FrameHeight}.");
Console.WriteLine($"RAM: {core.SystemRam.Length} bytes. The text screen at $0400:");
PrintTextScreen(core.SystemRam[0x0400..0x07E8]);
Console.WriteLine($"Environment commands not answered: {string.Join(", ", core.UnansweredEnvironmentCommands.Order())}");

// Prints the non-empty rows of a 40x25 text screen. Screen codes 1-26 are A-Z and 32-63 match ASCII;
// bit 7 only means the character is shown reversed.
static void PrintTextScreen(ReadOnlySpan<byte> screen)
{
    var characters = new char[40];
    for (var row = 0; row < 25; row++)
    {
        for (var column = 0; column < 40; column++)
        {
            var code = screen[row * 40 + column] & 0x7F;
            characters[column] = code switch { < 32 => (char)('@' + code), < 64 => (char)code, _ => '#' };
        }

        var line = new string(characters).TrimEnd();
        if (line.Length > 0)
            Console.WriteLine($"  {row,2}: {line}");
    }
}
