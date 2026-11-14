using System.Buffers.Binary;
using C64Bot.Vice;
using C64Bot.Vice.Protocol;

await using var client = await MonitorClient.ConnectAsync();
Console.WriteLine("Connected to VICE.");

var ping = await client.PingAsync();
Console.WriteLine($"Ping answered: request ID {ping.RequestId}, status {ping.Error}.");
await PrintEventsForAsync(TimeSpan.FromSeconds(1));

Console.WriteLine("VICE should be frozen now. Press Enter to resume it.");
Console.ReadLine();

var exit = await client.ExitAsync();
Console.WriteLine($"Exit answered: request ID {exit.RequestId}, status {exit.Error}.");
await PrintEventsForAsync(TimeSpan.FromSeconds(1));

Console.WriteLine("VICE should be running again, while still connected. Press Enter to disconnect.");
Console.ReadLine();

// Prints every event that has arrived, or arrives within the given time.
async Task PrintEventsForAsync(TimeSpan duration)
{
    using var window = new CancellationTokenSource(duration);
    try
    {
        await foreach (var received in client.Events.ReadAllAsync(window.Token))
            Console.WriteLine($"  Event: {Describe(received)}");
    }
    catch (OperationCanceledException)
    {
        // The time is up.
    }
}

static string Describe(MonitorResponse received) => received.Type switch
{
    ResponseType.Stopped or ResponseType.Resumed =>
        $"{received.Type}, PC = ${BinaryPrimitives.ReadUInt16LittleEndian(received.Body.Span):X4}",
    _ => $"{received.Type}, {received.Body.Length} bytes",
};
