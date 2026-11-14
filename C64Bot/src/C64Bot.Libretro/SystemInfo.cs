namespace C64Bot.Libretro;

/// <summary>
/// What a libretro core says about itself: its name, its version and the files it can load.
/// </summary>
/// <param name="ValidExtensions">The file extensions the core can load, separated by <c>|</c>.</param>
/// <param name="NeedFullPath">
/// Whether the core must be given a file path when loading a game, instead of the file's contents.
/// </param>
public sealed record SystemInfo(string LibraryName, string LibraryVersion, string ValidExtensions, bool NeedFullPath);
