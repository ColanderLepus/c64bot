# M4: Planner for levels without guards (solver idea 2)

**Status:** Started 2026-11-16. Steps agreed 2026-11-24; P1 done.

The first milestone where the bot plays on its own. It reads the level from RAM, plans a route to every piece of gold and then to the exit, and follows the route with the joystick, in real time. Guards come in M5. No level on the disk is without guards, so the bot starts levels with their guards taken out (P1).

## Tasks

- [ ] Model the level as a graph using the movement rules (walk, climb, rope, fall, dig and drop)
- [ ] BFS/A* to each piece of gold, then to the exit ladder
- [ ] Debug view showing the grid and the planned path
- [ ] Debug mode (moved from M1 T9): the real-time loop with a different rule, one frame per key press in the window, and a key to switch between that and real time. Check: each press advances the game by one frame, and holding the key runs it in slow motion.
- [ ] Test on real levels with unlimited lives on, starting each at the level under test with `GameStarter.StartLevel` (D13; no invincibility and no C=, D6)
- [x] Levels without guards: start any level with its guards taken out
- [ ] `IPlayer` and the loop that runs the bot (its shape was left to M3 or M4, and M3 was dropped)

## Steps

Proposed 2026-11-16, agreed 2026-11-24. Done one step at a time, checking each before starting the next. Claude checks each step headless; P4 and P5 say where the owner is needed.

- [x] **P1. Levels with any changes, from the buffer.** A level is read from the disk image or written as text, changed with chained methods (for example without its guards), and put into the game's level buffer when a new game starts, so the game sets it up without reading the disk (D13, D16; see "A level put into the buffer" in [lode-runner.md](../lode-runner.md)). Such a level starts 150 frames sooner, and runs from it repeat exactly. A change holds only for the level started: the next one comes from the disk. All of this is the test bench (D17). Its parts, each checked before the next:
  - [x] **1. `DiskImage`** (generic): reads a sector of a `.d64`. Done 2026-11-24. Checked: unit tests (sizes, the first or last sector of each zone of tracks, a file open elsewhere), and level 1's sector on the real disk.
  - [x] **2. `RamPatch` and `StartPoint`** (generic): a chained list of writes, and a saved moment to start from with a patch. `GameStarter` uses them, with no change in what it does. Done 2026-11-24. Checked: tests of the chaining, and with the core that a start brings back the saved moment with only the patched bytes changed; the existing tests pass unchanged.
  - [x] **3. `LevelData` and `LevelTile`**: a level as stored, immutable; from the disk, from the game's bytes or from text; changed with `With`, `Select`, `Replace` and `WithoutGuards`. Done 2026-11-24. Checked: unit tests; all 150 levels give back the same bytes, and their guards match "Guards per level" in lode-runner.md.
  - [x] **4. `GameStarter.StartLevel`** with a `LevelData`. Plain `StartLevel` is unchanged: the game loads the level from the disk. Done 2026-11-24. Checked with tests: levels 1 and 150 with their guards are set up the same as from the disk (grids, level buffer and work area, while waiting for the first move); level 1 without guards has none, also after a death (the player trapped in his own hole); two runs with the same input have the same RAM after each of 1,500 frames (level 1); a made-up level is set up as written; and after a completed made-up level, the next one comes from the disk. Without the write to `$133B`, four of these tests fail, as they should. Headless, all 150 levels without guards have no guards, the right gold, tiles and level number, and the player in place; pictures of levels 100 and 150 and a made-up level looked right.
  - [x] **5. The app's `noguards` argument.** `LevelData` also got its number here (`StartLevel(5)` is the game's own level 5; `StartLevel(level)` is a level of our own, which knows its number). Done 2026-11-24. Checked: the app run with no arguments, `5` and `5 noguards` (pictures of its window: level 5 with and without guards), and six wrong ones, which give a message.
- [ ] **P2. Movement rules.** A model of where the player can go on a level and how: walking left and right, climbing ladders, moving along ropes, letting go of a rope, and falling, taken from the game's movement code in the disassembly. No digging yet (P6). Check: unit tests on small made-up levels; and with the core as the judge, on a set of guard-free levels, every move from every tile the player can reach ends where the model says.
- [ ] **P3. Planner without digging.** A path search (BFS) over the model: to the nearest gold, then the next, and once all gold is taken, up the exit ladders to the top row. Check: unit tests on made-up levels, and the list of levels whose gold and exit the planner can reach without digging.
- [ ] **P4. The bot plays.** `IPlayer` and the loop that runs it (open point 1; it gets only the game state and returns input, D17), and a bot that follows the planned route with the joystick, reading the game state after every frame. The app gets a way to let the bot play a level (with R2, open point 2). Check: headless, the bot completes the guard-free levels P3 found it can; then **the owner watches the bot play a level in the window** (the first time the bot plays, in real time).
- [ ] **P5. Debug view and debug mode.** The grid and the planned route drawn over the game's picture, and one frame per key press with a key to switch back to real time (from M1 T9). Check: Claude saves pictures with the route drawn on; **the owner tries the stepping** in the window (needs a person).
- [ ] **P6. Digging.** The model learns digging: fire digs the brick diagonally below on the side of the last move, the hole opens about 20 frames later, lasts 180 passes, and kills the player if it closes on him. The planner uses holes to get down through bricks, and the bot digs. Check: unit tests, and headless the bot completes more guard-free levels.
- [ ] **P7. All levels.** The bot plays all 150 levels without guards, headless. The results go in Notes (levels completed, and why the others failed), and failures within reach are fixed. Check: the count; the owner decides when it's good enough to move on to M5.

## Open points

To settle before P4 (Claude's suggestion first). The one about writing the level's data is settled: D13 and D16.

1. **`IPlayer`'s shape** (P4). Suggestion: the loop calls the bot on the emulation thread after every frame, with the game state (none in a frame that can't be read, D14), and the bot answers at once with the joystick for the next frame. A path search on a 28×16 level takes microseconds, so M4's bot can plan right there. A bot that needs more time (M5, M6) thinks on a thread of its own and hands in its latest decision, as the Architecture section in `PLAN.md` says.
2. **R2.** Suggestion: split up `Program.cs` in P4, when the app gets the bot.

## Notes

- **Research for P1** (headless, 2026-11-16): no level on the disk is without guards; the game builds a level's guards from the guard tiles in its data; and a level written into the game's level buffer is set up without the disk, the same as from the disk, 150 frames sooner, and repeatably. The details are in [lode-runner.md](../lode-runner.md): "Guards per level", "Levels on the disk", "How a level is set up" and "A level put into the buffer".
