using C64Bot.Libretro;

const string CorePath = @"C:\Tools\C64\libretro\vice_x64sc_libretro.dll";

using var core = LibretroCore.Load(CorePath);
Console.WriteLine($"libretro API version: {core.ApiVersion}");

var info = core.GetSystemInfo();
Console.WriteLine($"Core: {info.LibraryName} {info.LibraryVersion}");
Console.WriteLine($"Needs a file path: {info.NeedFullPath}");
Console.WriteLine($"Files it can load: {info.ValidExtensions}");
