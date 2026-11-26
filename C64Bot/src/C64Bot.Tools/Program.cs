using System.Diagnostics;
using C64Bot.Libretro;
using C64Bot.LodeRunner;

// Tools for looking at the game outside the window:
//   picture <file>    turns a level or a state written as text (see LevelPicture) into a picture: the same name,
//                     ending in .png, next to the text. Nothing is run.
//   pictures <level>  plays the level without its guards with the planner, route by route, and saves a picture at the
//                     start, after each route, and where it ends, in pictures\level<level> under the folder it's run in.
const string CorePath = @"C:\Tools\C64\libretro\vice_x64sc_libretro.dll";
const string DiskPath = @"C:\Emulation\ROMs\C64\Lode Runner.d64";

if (args is ["picture", var textFile])
{
    var png = Path.ChangeExtension(textFile, ".png");
    File.WriteAllBytes(png, LevelPicture.Png(File.ReadAllText(textFile)));
    Console.WriteLine($"Picture in {Path.GetFullPath(png)}");
    return;
}

if (args.Length != 2 || args[0] != "pictures" || !int.TryParse(args[1], out var number)
    || number is < 1 or > Level.MaxNumber)
{
    Console.WriteLine($"Use: picture <text file>, or pictures <level> with a level from 1 to {Level.MaxNumber}.");
    return;
}

var folder = Path.Combine("pictures", $"level{number}");
Directory.CreateDirectory(folder);
foreach (var old in Directory.GetFiles(folder, "*.png"))
    File.Delete(old);

using var core = C64Core.Load(CorePath, DiskPath, Path.GetDirectoryName(CorePath)!, joystickPort: 2);
var starter = GameStarter.Start(core, unlimitedLives: true);
starter.StartLevel(new LevelDisk(DiskImage.Load(DiskPath))[number].WithoutGuards());
GameState? state = null;
for (var frame = 0; frame < 1200 && state is not { Alive: true, Completed: false }; frame++)
{
    core.RunFrame();
    GameState.TryRead(core.SystemRam, out state);
}

if (state is null)
{
    Console.WriteLine("The level wasn't set up.");
    return;
}

Save(0, "start", state);
Console.WriteLine($"Level {number} without its guards: {state.GoldLeft} gold, he's at {Name(state.Player.Position)}.");
for (var routes = 1; ; routes++)
{
    var clock = Stopwatch.StartNew();
    var route = Route.Find(state);
    if (route is null)
    {
        Save(routes, "stuck", state, new LevelGraph(state).WithinReach(state));
        Console.WriteLine(
            $"Stuck at {Name(state.Player.Position)} with {state.GoldLeft} gold left: no route found in " +
            $"{clock.ElapsedMilliseconds} ms. Dots mark the tiles within reach.");
        break;
    }

    state = route.End;
    var digs = route.Moves.Count(move => move.Kind is MoveKind.DigLeft or MoveKind.DigRight);
    Console.WriteLine(
        $"Route {routes}: {clock.ElapsedMilliseconds} ms to find, {route.Passes.Count} passes, {digs} digs, " +
        $"to {Name(state.Player.Position)}, {state.GoldLeft} gold left.");
    if (state.Completed)
    {
        Save(routes, "completed", state);
        Console.WriteLine("Completed.");
        break;
    }

    Save(routes, $"route{routes}", state);
}

Console.WriteLine($"Pictures in {Path.GetFullPath(folder)}");

// A tile as the pictures name it: its row's letter, then its column.
static string Name(TilePosition tile) => $"{(char)('A' + tile.Row)}{tile.Column}";

void Save(int order, string name, GameState shown, IReadOnlySet<TilePosition>? marked = null) =>
    File.WriteAllBytes(Path.Combine(folder, $"{order:D2}-{name}.png"), LevelPicture.Png(shown, marked));
