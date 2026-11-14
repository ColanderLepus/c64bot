using System.Buffers.Binary;
using System.Diagnostics;
using C64Bot.Vice;
using C64Bot.Vice.Protocol;

const int Seconds = 10;
const double JiffiesPerSecond = 60.0;

await using var client = await MonitorClient.ConnectAsync();
Console.WriteLine("Connected to VICE.");

// The C64 runs its interrupt routine through the vector at $0314-$0315 (low byte first).
var vector = await client.MemoryGetAsync(0x0314, 0x0315);
var interruptRoutine = (ushort)(vector[0] | vector[1] << 8);
Console.WriteLine($"Interrupt routine at ${interruptRoutine:X4}.");

// An exec checkpoint on the interrupt routine's first instruction: stop when hit, enabled, exec,
// not temporary, main memory.
var checkpointBody = new byte[9];
BinaryPrimitives.WriteUInt16LittleEndian(checkpointBody.AsSpan(0, 2), interruptRoutine);
BinaryPrimitives.WriteUInt16LittleEndian(checkpointBody.AsSpan(2, 2), interruptRoutine);
checkpointBody[4] = 0x01;
checkpointBody[5] = 0x01;
checkpointBody[6] = 0x04;
checkpointBody[7] = 0x00;
checkpointBody[8] = (byte)Memspace.MainMemory;

var checkpointSet = await client.SendAsync(CommandType.CheckpointSet, checkpointBody);
Console.WriteLine($"Checkpoint set: status {checkpointSet.Error}");
if (checkpointSet.Error != ResponseError.Ok)
    return;

var checkpointNumber = checkpointSet.Body[..4];

try
{
    await client.ExitAsync();
    await RunLockstepAsync();
}
finally
{
    // A checkpoint stays in VICE after we disconnect, and would stop the emulator at the next interrupt
    // with nobody there to resume it.
    var deleted = await client.SendAsync(CommandType.CheckpointDelete, checkpointNumber);
    await client.ExitAsync();
    Console.WriteLine($"Checkpoint deleted: status {deleted.Error}");
}

async Task RunLockstepAsync()
{
    Console.WriteLine($"Running in lockstep for {Seconds} seconds: stop at each interrupt, read 4 KB, Exit when due ...");

    var responseTimes = new List<double>();
    var holdTimes = new List<double>();
    var intervals = new List<double>();
    TimeSpan? firstStop = null, lastStop = null;
    int firstJiffies = 0, lastJiffies = 0;
    var checkpointHit = false;

    var clock = Stopwatch.StartNew();

    while (clock.Elapsed < TimeSpan.FromSeconds(Seconds))
    {
        // A checkpoint hit arrives as three events: checkpoint info, register info, stopped.
        // The game may stop running this interrupt routine (for example between levels), so give up
        // after a while without one.
        MonitorResponse monitorEvent;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            monitorEvent = await client.Events.ReadAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"No stop for 3 seconds, after {clock.Elapsed.TotalSeconds:F1} s. Measuring what we have.");
            break;
        }

        if (monitorEvent.Type == ResponseType.CheckpointInfo)
            checkpointHit = true;

        if (monitorEvent.Type != ResponseType.Stopped || !checkpointHit)
            continue;

        checkpointHit = false;
        var stopped = clock.Elapsed;
        if (lastStop is { } previous)
            intervals.Add((stopped - previous).TotalMilliseconds);

        var memory = await client.MemoryGetAsync(0x0000, 0x0FFF);
        responseTimes.Add((clock.Elapsed - stopped).TotalMilliseconds);

        // The KERNAL's jiffy clock: $A0-$A2, high byte first, counted up 60 times per emulated second.
        var jiffies = memory[0xA0] << 16 | memory[0xA1] << 8 | memory[0xA2];
        if (firstStop is null)
        {
            firstStop = stopped;
            firstJiffies = jiffies;
        }

        lastStop = stopped;
        lastJiffies = jiffies;

        // Pace the game ourselves: hold the emulator until as much real time has passed since the first
        // stop as emulated time has. Spinning, because Windows' normal timers only tick every 15.6 ms.
        var due = firstStop.Value + TimeSpan.FromSeconds((jiffies - firstJiffies) / JiffiesPerSecond);
        var holdStart = clock.Elapsed;
        while (clock.Elapsed < due)
            Thread.SpinWait(100);
        holdTimes.Add((clock.Elapsed - holdStart).TotalMilliseconds);

        await client.ExitAsync();
    }

    if (responseTimes.Count < 2)
    {
        Console.WriteLine($"Only {responseTimes.Count} stops: too few to measure.");
        return;
    }

    var real = (lastStop - firstStop)!.Value.TotalSeconds;
    var emulated = (lastJiffies - firstJiffies) / JiffiesPerSecond;

    Console.WriteLine($"Stops: {responseTimes.Count} ({responseTimes.Count / clock.Elapsed.TotalSeconds:F1} a second)");
    Console.WriteLine($"Reading 4 KB after a stop: average {responseTimes.Average():F2} ms, worst {responseTimes.Max():F2} ms");
    Console.WriteLine($"Held back before Exit: average {holdTimes.Average():F2} ms, longest {holdTimes.Max():F2} ms");
    Console.WriteLine($"Time between stops: average {intervals.Average():F2} ms, shortest {intervals.Min():F2} ms, longest {intervals.Max():F2} ms");
    Console.WriteLine($"Emulated time: {emulated:F2} s in {real:F2} s of real time ({emulated / real:P0} speed)");
}
