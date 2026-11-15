using C64Bot.ViceMonitor.Protocol;

namespace C64Bot.ViceMonitor;

/// <summary>
/// The exception thrown when VICE answers a command with an error code instead of OK.
/// </summary>
public sealed class MonitorCommandException(CommandType command, ResponseError error)
    : Exception($"VICE answered {command} with error {error}.")
{
    public CommandType Command { get; } = command;
    public ResponseError Error { get; } = error;
}
