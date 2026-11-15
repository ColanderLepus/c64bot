namespace C64Bot.ViceMonitor.Protocol;

/// <summary>
/// Whether a command succeeded, and if not, why.
/// </summary>
/// <remarks>
/// Received as byte 7 of the response header. See section 13.2 of the VICE manual.
/// </remarks>
public enum ResponseError : byte
{
    Ok = 0x00,

    /// <summary>
    /// The thing the command tried to get or set doesn't exist, for example an unknown checkpoint number.
    /// </summary>
    ObjectMissing = 0x01,

    /// <summary>
    /// The command named a memspace that doesn't exist. A memspace says whose memory is meant: the
    /// C64's own, or that of one of the emulated disk drives.
    /// </summary>
    InvalidMemspace = 0x02,

    /// <summary>
    /// The body length is wrong for this command type.
    /// </summary>
    InvalidLength = 0x80,

    InvalidParameter = 0x81,
    UnsupportedApiVersion = 0x82,
    UnknownCommand = 0x83,

    /// <summary>
    /// The command looked valid, but carrying it out failed.
    /// </summary>
    GeneralFailure = 0x8F,
}
