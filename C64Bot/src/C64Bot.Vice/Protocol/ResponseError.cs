namespace C64Bot.Vice.Protocol;

/// <summary>
/// Error codes in byte 7 of a response header (VICE manual section 13.2).
/// </summary>
public enum ResponseError : byte
{
    Ok = 0x00,
    ObjectMissing = 0x01,
    InvalidMemspace = 0x02,
    InvalidLength = 0x80,
    InvalidParameter = 0x81,
    UnsupportedApiVersion = 0x82,
    UnknownCommand = 0x83,
    GeneralFailure = 0x8F,
}
