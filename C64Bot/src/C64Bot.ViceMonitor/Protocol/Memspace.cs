namespace C64Bot.ViceMonitor.Protocol;

/// <summary>
/// Whose memory a command means: the C64's own, or that of one of the emulated disk drives.
/// </summary>
/// <remarks>
/// See section 13.4.1 of the VICE manual.
/// </remarks>
public enum Memspace : byte
{
    MainMemory = 0x00,
    Drive8 = 0x01,
    Drive9 = 0x02,
    Drive10 = 0x03,
    Drive11 = 0x04,
}
