using C64Bot.Vice;

await using var client = await MonitorClient.ConnectAsync();
Console.WriteLine("Connected to VICE.");

// The text screen starts at $0400: 25 rows of 40 bytes, one screen code per character.
// The second row ($0428-$044F) holds the start-up banner.
var bannerRow = await client.MemoryGetAsync(0x0428, 0x044F);
Console.WriteLine($"Banner row: {string.Join(" ", bannerRow.Select(code => code.ToString("X2")))}");

// Screen code 1 is the letter A.
await client.MemorySetAsync(0x0400, new byte[] { 0x01 });
Console.WriteLine("Wrote screen code 1 (A) to $0400.");

await client.ExitAsync();
Console.WriteLine("VICE resumed. The top-left corner of its screen should show an A.");
