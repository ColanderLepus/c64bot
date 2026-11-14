using C64Bot.Vice;

await using var client = await MonitorClient.ConnectAsync();
Console.WriteLine("Connected to VICE.");

var response = await client.PingAsync();
Console.WriteLine($"Ping answered: request ID {response.RequestId}, status {response.Error}.");
