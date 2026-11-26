using C64Bot.Libretro;
using C64Bot.Libretro.Tests;
using Xunit.Abstractions;

namespace C64Bot.LodeRunner.Tests;

[Collection(LodeRunnerCollection.Name)]
public class RaceTests(LodeRunnerReadyToStart lodeRunner, ITestOutputHelper output)
{
    // Small, closed-off rooms where a lot can happen in a short time, each with the frames to play in it and how many
    // times. Gold behind the walls keeps the levels from being completed, except in the last one.
    private static readonly (string Text, int Frames, int Runs)[] Rooms =
    [
        // Ropes with bricks at their ends, one above another, gold and trap bricks to fall through, and ladders.
        ("""
        @@@@@@@@@@
        @HP---  H@
        @H##  ##H@
        @H  -$  H@ $
        @H#TT###H@
        @H $$   H@
        @########@
        @@@@@@@@@@
        """, 380, 6),

        // Bricks to dig.
        ("""
        @@@@@@
        @ P  @
        @####@ $
        @####@
        @@@@@@
        """, 300, 2),

        // The top left corner.
        ("""
          P @
        H#H#@
        H H @ $
        ####@
        """, 120, 2),

        // The bottom right corner.
        (new string('\n', 12) + """
                                @@@@
                                @  H
                                @##H
                              $ @P H
        """, 120, 2),

        // The exit.
        ("""
        @  h@
        @P$h@
        @###@
        """, 200, 3),
    ];

    // What a hole that closes leads to has a test of its own, because it takes long.
    private static readonly Race.Situation[] HoleSituations =
        [Race.Situation.HoleClosed, Race.Situation.HoleClosedBesideOrBelowHim, Race.Situation.DiedInAHole];

    private LibretroCore Core => lodeRunner.Core;

    [CoreFact]
    public void Next_RandomPlayInMadeUpRooms_IsWhatTheGameDoes()
    {
        // Arrange
        var race = new Race(Core, seed: 2);
        var differences = new List<string>();

        // Act: each run starts from the same saved moment
        foreach (var (text, frames, runs) in Rooms)
        {
            lodeRunner.Starter.StartLevel(Level.Parse(1, text));
            lodeRunner.RunUntilSetUp();
            var start = Core.SaveState();
            for (var run = 0; run < runs; run++)
            {
                Core.LoadState(start);
                if (race.Run(frames) is { } difference)
                    differences.Add(difference);
            }
        }

        // Assert
        Report(race);
        Assert.Empty(differences);
        Assert.Empty(Enum.GetValues<Race.Situation>().Except(HoleSituations).Except(race.Seen.Keys));
    }

    [CoreFact]
    public void Next_HoleClosesNearHim_IsWhatTheGameDoes()
    {
        // Arrange: he digs a hole to his right and waits until it's about to close
        var race = new Race(Core, seed: 1);
        lodeRunner.Starter.StartLevel(Level.Parse(1, """
            @@@@@
            @P  @ $
            @####
            @@@@@
            """));
        lodeRunner.RunUntilSetUp();
        RunUntil(state => state.Player.OffsetX > 0, RetroPadButton.Right);
        RunUntil(state => state.Holes.Count > 0, RetroPadButton.B);
        RunUntil(state => state.Holes[0].PassesLeft <= 24);
        var aboutToClose = Core.SaveState();

        // Act: he walks to it when it has so many passes left, or stays beside it
        var differences = new List<string>();
        for (var passesLeft = 0; passesLeft <= 22; passesLeft++)
        {
            Core.LoadState(aboutToClose);
            var difference = race.Run(
                frames: 60,
                state => state.Holes.Any(hole => hole.PassesLeft > passesLeft) ? Joystick.None : Joystick.Right);
            if (difference is not null)
                differences.Add(difference);
        }

        // Assert
        Report(race);
        Assert.Empty(differences);
        Assert.Empty(HoleSituations.Except(race.Seen.Keys));
    }

    // Takes minutes, so it's only run when it's asked for.
    [FullRaceFact]
    public void Next_RandomPlayOnAllLevels_IsWhatTheGameDoes()
    {
        // Arrange
        var race = new Race(Core, seed: 5);
        var differences = new List<string>();

        // Act
        for (var number = 1; number <= Level.MaxNumber; number++)
        {
            lodeRunner.Starter.StartLevel(lodeRunner.Levels[number].WithoutGuards());
            lodeRunner.RunUntilSetUp();
            if (race.Run(frames: 600) is { } difference)
                differences.Add($"Level {number}: {difference}");
        }

        // Assert
        Report(race);
        Assert.Empty(differences);
    }

    private void RunUntil(Func<GameState, bool> condition, RetroPadButton? held = null)
    {
        if (held is { } pressed)
            Core.PressButton(pressed);
        var happened = false;
        for (var frame = 0; frame < 400 && !happened; frame++)
        {
            Core.RunFrame();
            happened = GameState.TryRead(Core.SystemRam, out var state) && condition(state);
        }

        if (held is { } released)
            Core.ReleaseButton(released);
        Assert.True(happened, "It didn't happen.");
    }

    private void Report(Race race)
    {
        foreach (var situation in Enum.GetValues<Race.Situation>())
            output.WriteLine($"{race.Seen.GetValueOrDefault(situation),6}  {situation}");
    }
}
