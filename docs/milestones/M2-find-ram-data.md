# M2: Find Lode Runner's data in RAM

**Status:** Done, 2026-11-16 (started 2026-11-15; F1, F2 and F4–F11 done, F3 dropped)

**Result:** `GameState` in `C64Bot.LodeRunner` reads everything the bot needs from RAM after each frame: the level, the player, the guards, the holes and the counters, each from where the game keeps it complete (D14); `GamePhaseReader` tells what the game is doing; `GameStarter` starts a new game at any level in about a second (D13). The RAM map in [lode-runner.md](../lode-runner.md) says how each address was confirmed.

Find where Lode Runner keeps what the bot needs in the C64's RAM: the game's state, the level, the player, the guards, the holes, and the counters. RAM comes straight from the core after each frame (`LibretroCore.SystemRam`), and save states let us try the same moment again with different inputs. Findings go in the RAM map in [lode-runner.md](../lode-runner.md).

## Tasks

- [x] Build the RAM-compare tool (F1)
- [x] First target: the game's state (title screen, demo, playing, level ready), so start-up no longer depends on fixed frame numbers (M1 T7) (F4, F5)
- [x] Half-updated state: RAM is read when `retro_run` returns, at the end of a frame, which can fall in the middle of the game's update. Find out whether that happens, and how to read a complete state (F6)
- [x] Level grid (28×16) and tile codes (F7)
- [x] Player position, including position within a tile (F8)
- [x] Guard positions and states (F9)
- [x] Gold remaining, lives, level number (F10)
- [x] Hole/dig state and refill timers (F10)
- [x] Find out how the C64 version maps digging (fire button plus direction, or keys) (F9, F10)
- [x] Record findings in the RAM map in [lode-runner.md](../lode-runner.md) (F2–F11)

## Steps

Agreed with the owner on 2026-11-15 and done one step at a time. The addresses came from the disassembly (see "The disassembly" in [lode-runner.md](../lode-runner.md)), and each step confirmed the ones it used. Generic code went in `C64Bot.Libretro`, Lode Runner code in `C64Bot.LodeRunner` (D12). From F7 on, Claude checked each step itself, headless, against the core's pictures saved as PNG files.

- [x] **F1. RAM-compare tool** (in `C64Bot.Libretro`). `RamImage` compares two RAM images and lists the changed ranges with old and new values, optionally limited to a chosen range, and saves and loads RAM as a file; `PrgFile` loads a `.prg` file at its load address. In the app's console, R remembers RAM and D prints what changed since then. Checked with unit tests, and in the demo: the changes were where the disassembly puts the moving parts.
- [x] **F2. Is version A the original?** Version A's `$6000`–`$B7FF` during a level, compared with version B's `it` and the disassembly's `it.prg` (kept outside the repo): every difference is explained, by the trainer's three changes or by code that changes itself. So the disassembly's addresses apply, and its RAM map went into `lode-runner.md`, unconfirmed until a step checked it.
- **F3. Trace addresses per frame.** Dropped (owner's call, 2026-11-15): it was meant for searching, and the disassembly already says what the bytes mean.
- [x] **F4. Game state.** `GamePhaseReader` in the new `C64Bot.LodeRunner` reads a `GamePhase` after every frame: `Other` (until the game's interrupt routine is in place, or the level editor), `Title`, `Demo`, `LevelStarting` (everything between two stretches of play while the player is alive), `Playing` (the main loop runs: the interrupt counter `$132D` keeps dropping back to 3–5), `Dying` (dead during play, lives left) and `GameOver` (dead, no lives left). A completed level shows as the level number `$1305` going up; it isn't a phase, because RAM doesn't tell it apart from setting up the next level. The app prints each phase change and each level number. Checked headless to game over, and by the owner playing in the app.
- [x] **F5. Start-up and level picking.** `GameStarter.Start` runs flat out from loading through the trainer menu into the running demo, presses fire, and saves a state at the first frame of the new game, before it loads its level. `StartLevel(n)` loads that state and writes the level number (`$1310` = n − 1, `$1305` = n) with `LibretroCore.WriteSystemRam`, so the game loads level n itself (D13) and waits for the first move. The app starts at the level given on its command line, level 1 if none. Checked with tests (level 5 after level 1 gives a different level with the right numbers), headless on levels 1, 2, 5 and 150, and by the owner at level 5. `Start` pressed SPACE until 2026-11-16, which made a picked level start by itself (see "What starts a waiting level" in `lode-runner.md`).
- [x] **F6. Half-updated state.** Measured headless where frames end (see Notes): most end inside a pass, but little is half done, so the owner chose no fix: each thing is read from where it's always complete (D14, "Reading RAM" in `lode-runner.md`).
- [x] **F7. Level grid.** `Level`, `Tile` and `TilePosition`: 28×16 tiles, read from the grid as built (`$0A00`, D14), with the hidden exit ladders. The app's G key prints the game state. Checked on all 150 levels (only known tiles, and the gold adds up to the gold count), and against pictures of levels 1, 5 and 150; digging changed only the grid as played, and the exit ladders appeared once all gold was taken.
- [x] **F8. Player.** `Player` (with `Pose` and `Direction`) and the first `GameState`: `GameState.TryRead` reads the level and the player, or gives false for a frame to skip (D14), and `GameState.ToString` draws the level as on screen (`Level.ToString` stays the bare layout, owner's choice). Checked against pictures: walking, climbing, the rope, falling and digging.
- [x] **F9. Guards.** `Guard`, read from the guard tables (D14): number, tile, offsets, pose and facing from his sprite, and `CarriesGold`, `InHole` and `Respawning` from his two timers. `GameState.Guards`, drawn as `G` (`g` while waiting to respawn). Checked against pictures: a guard picked up gold, fell into a hole, died and respawned where the game's rule says.
- [x] **F10. Holes and counters.** `Hole`, and `GameState` gets `Holes` (drawn as `o`), `LevelNumber`, `Lives`, `Score` and `GoldLeft`. `GamePhaseReader.Phase` gives the last phase without counting a frame, because `Update` must be called exactly once per frame. Checked against pictures: digging on both sides, a hole from dig to close, losing a life, taking gold and skipping a level.
- [x] **F11. Finish.** Every entry in the RAM map says how it was confirmed, or that it wasn't and the bot doesn't use it; a last headless run on level 1 checked the ones left. Out-of-date statements were corrected, and M2 closed.

## Notes

The notes from each step's checks were trimmed on 2026-11-16: what they found is in [lode-runner.md](../lode-runner.md), and the notes themselves are in Git history. The F6 measurements stay here, as the grounds for D14.

- **F6, where frames end** (headless, 2026-11-15, a throwaway script; random joystick input in the levels): the CPU's PC at the end of each frame, read from the save state (the VICE core saves VICE's snapshot format, whose `MAINCPU` module holds the registers; saving a state costs about 0.07 ms), and for frames that end in the interrupt, the interrupted PC from the stack. Counted over frames with the main loop running:

  | | Demo, 3 guards | Level 5, 4 guards | Level 150, 5 guards |
  | --- | --- | --- | --- |
  | Frames measured | 6,000 | 3,000 | 3,865 |
  | Frame ends in a pass (including interrupted passes) | 55% | 62% | 62% |
  | Frame ends in the interrupt routine | 2% | 2% | 2% |
  | The guard being moved (`$131F`) differs from his table entry | 11% | 13% | 15% |
  | A guard's table entry has no guard tile in the grid, in a pass | 3% | 4% | 3% |
  | No player tile in the grid at `$03`/`$04` | 0.1% | 0.03% | 0.03% |

  Most of a pass's time is spent at `$80xx`–`$8Dxx`, which by its place in the source is drawing. The guard tables are written in one go after a guard's move (`EnemyMoveStatusSave`), so they're always complete, but up to one move behind for the guard being moved; the grid's guard tiles can be ahead of them. In the demo, 1.5% of frames in the wait also had a guard without a guard tile, so that's a game situation, not a half update.
