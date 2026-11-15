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

- Joystick in **port 2** (according to version B's loading screen; confirmed for version A by the owner, and by the player walking right with the joystick in port 2 in the libretro core, 2026-11-15).
- Undiggable blocks look different from diggable bricks. On level 1, the left part of the top brick row is undiggable.
- **Interrupt routine** (seen 2026-11-14, during a level): the vector at `$0314`–`$0315` points to the game's own routine at `$648A`. It runs about 60 times per emulated second, so it's driven by the CIA timer, not by the screen. Once, it didn't run for 1.2 s. The KERNAL's jiffy clock (`$A0`–`$A2`) keeps counting during the game, so it's a reliable measure of emulated time. Not checked: whether the vector changes between levels or in the menus.
- **Trainer menu and level start** (seen 2026-11-15 in the libretro core): the trainer menu appears 2,532 frames after loading starts, its text in the screen memory at `$0400` (row 2 `UNLIMITED LIVES (Y/N) ?`, row 4 `TOTAL INVINCIBILITY (Y/N) ?`, row 7 `C= SKIPS LEVEL DURING GAME`, row 10 `(SPACE)`). While the menu waits, the interrupt vector points to the KERNAL's own routine, `$EA31`. Right after SPACE it points to `$648A`.
- **Title screen and demo** (seen 2026-11-15): after the trainer menu the game shows its title screen, then plays itself (a demo). The demo is running by about frame 1000 after the menu (20 s). Pressing SPACE during the demo starts the real game; fire doesn't (checked in the libretro core: fire on port 2 at frame 1000 left the demo running). The interrupt vector already points to `$648A` at the title screen, so it doesn't tell the title, the demo and the game apart. When a level starts, the interrupt routine stops for about 1.5 s, so the jiffy clock stands still while the level is set up.
- **Invincibility breaks the demo** (seen 2026-11-15 in the libretro core): with the trainer's invincibility on, the demo player gets stuck after about 5 seconds (at the bottom of the left ladder of the demo level, with a guard on top of him), and from then on SPACE no longer starts the game. With invincibility off, the demo keeps playing and SPACE works. Unlimited lives doesn't break it. Presumably the demo replays recorded moves, and a player who can't be caught falls out of step with them.
- **Starting a level** (seen 2026-11-15 in the libretro core, invincibility off): after SPACE, the screen goes black with the score at 0, then the level builds up bit by bit for about 10 seconds. A level 1 different from the demo's appears. A joystick direction makes the player move; nobody touching the joystick ends with the player caught by a guard. Holding right from 2 seconds after SPACE onwards made the player walk right along the bottom row and pick up gold once the level was ready.
- **Joystick port 2 as the C64 sees it** (`$DC00`, read in the libretro core, 2026-11-15): nothing `$7F`, right `$77`, left `$7B`, up `$7E`, fire `$6F`. A pressed direction or fire pulls its bit low: bit 0 up, bit 1 down, bit 2 left, bit 3 right, bit 4 fire.

## RAM map

Filled in during M2. Addresses are for version A.

| Address | Size | Meaning | Notes |
| --- | --- | --- | --- |
| | | | |
