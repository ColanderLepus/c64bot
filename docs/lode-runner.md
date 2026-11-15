# Lode Runner reference

What we know about the C64 Lode Runner we use: the versions on the disk, how the game behaves, and where it keeps its data in RAM. This is reference material for every milestone, so it lives outside the milestone documents.

## Versions on the disk

`C:\Emulation\ROMs\C64\Lode Runner.d64` holds **three versions** of the game (files from `c1541 -list`, load addresses from the file headers). We use **version A** (D6). Stick with it, because RAM addresses can differ between versions.

| Version | Files | What we know |
| --- | --- | --- |
| A | `loderunner+3m/pw` (63 blocks, $0801) | First file on the disk, so `LOAD"*",8,1` (or autostarting the image without a file name) loads this one. Starts with a trainer menu: `UNLIMITED LIVES (Y/N)?`, `TOTAL INVINCIBILITY (Y/N)?`, a note that the C= key skips levels during the game, then press SPACE. Tried 2026-11-14 with Y/Y: the status line is clean. |
| B | `loderunner i` (3 blocks, $0801) + `it` (89 blocks, $6000) + `db` (12 blocks, $1400) | Crack by "M & J", January 1984. `loderunner i` is a BASIC loader: it shows a loading screen ("plug joystick into port #2"), then a small machine-code routine (POKEd into screen memory and hidden by black-on-black colour) loads `it` and `db` and jumps to $6000. So `it` is the main program and `db` is data of some kind. |
| C | `lode runner` (73 blocks, $0801) | Tried first (2026-11-14, `-autostart "...Lode Runner.d64:lode runner"`). Plays fine, but the status line at the bottom has stray white pixels. |

## Game notes

Observations about the game itself, gathered while playing.

- Joystick in **port 2** (according to version B's loading screen).
- Undiggable blocks look different from diggable bricks. On level 1, the left part of the top brick row is undiggable.
- **Interrupt routine** (seen 2026-11-14, during a level): the vector at `$0314`–`$0315` points to the game's own routine at `$648A`. It runs about 60 times per emulated second, so it's driven by the CIA timer, not by the screen. Once, it didn't run for 1.2 s. The KERNAL's jiffy clock (`$A0`–`$A2`) keeps counting during the game, so it's a reliable measure of emulated time. Not checked: whether the vector changes between levels or in the menus.
- **Trainer menu and level start** (seen 2026-11-15 in the libretro core): the trainer menu appears 2,532 frames after loading starts, its text in the screen memory at `$0400` (row 2 `UNLIMITED LIVES (Y/N) ?`, row 4 `TOTAL INVINCIBILITY (Y/N) ?`, row 7 `C= SKIPS LEVEL DURING GAME`, row 10 `(SPACE)`). While the menu waits, the interrupt vector points to the KERNAL's own routine, `$EA31`. Right after SPACE it points to `$648A`. When a level starts, the interrupt routine stops for about 1.5 s, so the jiffy clock stands still while the level is set up.

## RAM map

Filled in during M2. Addresses are for version A.

| Address | Size | Meaning | Notes |
| --- | --- | --- | --- |
| | | | |
