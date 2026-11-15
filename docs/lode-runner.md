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

Observations about the game itself, gathered while playing and in the libretro core. Frame numbers count from the moment SPACE is pressed in the trainer menu, at 50.125 frames a second. "Not checked" marks what was inferred rather than seen.

### Start-up

- **Loading** (libretro core, 2026-11-15): the trainer menu appears 2,532 frames, about 50 emulated seconds, after loading starts.
- **Trainer menu** (libretro core, 2026-11-15): its text is in the screen memory at `$0400`: row 2 `UNLIMITED LIVES (Y/N) ?`, row 4 `TOTAL INVINCIBILITY (Y/N) ?`, row 7 `C= SKIPS LEVEL DURING GAME`, row 10 `(SPACE)`. Each answer is one key press, Y or N, and SPACE ends the menu. While the menu waits, the interrupt vector at `$0314`–`$0315` points to the KERNAL's own routine, `$EA31`; right after SPACE it points to the game's, `$648A`.
- **Title screen and demo** (2026-11-15): after the menu the game shows its title screen, then plays itself (the demo). The demo is running by about frame 1000 (20 s). Pressing SPACE during the demo starts the real game; fire doesn't (fire on port 2 at frame 1000 left the demo running, in the libretro core). The interrupt vector points to `$648A` from the title screen on, so it doesn't tell the title, the demo and the game apart.
- **Invincibility breaks the demo** (libretro core, 2026-11-15): with the trainer's invincibility on, the demo player gets stuck after about 5 seconds (at the bottom of the left ladder of the demo level, with a guard on top of him), and from then on SPACE no longer starts the game. With invincibility off, the demo keeps playing and SPACE works. Unlimited lives doesn't break it. Presumably the demo replays recorded moves, and a player who can't be caught falls out of step with them (not checked).
- **Starting a level** (libretro core, invincibility off, 2026-11-15): after SPACE the screen goes black with the score at 0, then the level builds up bit by bit for about 10 seconds, and a level 1 different from the demo's appears. While the level is set up, the interrupt routine stops for about 1.5 s, so the jiffy clock stands still. A joystick direction then makes the player move: holding right from frame 1100 on made him walk right along the bottom row and pick up gold once the level was ready. In a run where nobody touched the joystick, a man was lost by frame 1990, presumably because a guard caught the player standing still (not checked).

### Controls

- **Joystick in port 2**, as version B's loading screen says. Confirmed for version A by the owner, and by the player walking right with the joystick in port 2 in the libretro core (2026-11-15).
- **The joystick as the C64 sees it** (`$DC00`, read in the libretro core, 2026-11-15): nothing `$7F`, right `$77`, left `$7B`, up `$7E`, fire `$6F`. A pressed direction or fire pulls its bit low: bit 0 up, bit 2 left, bit 3 right, bit 4 fire, and bit 1 down (not measured; the standard C64 layout).
- **SPACE** starts the game from the demo (see Start-up).
- **C= skips to the next level** during the game, as the trainer menu says. In our window that's Left Ctrl, which the libretro core maps to C= (tried by the owner, 2026-11-15).
- **Digging** works in our window, also while the player is running (owner, 2026-11-15). Exactly which inputs dig, and in which direction, is to be written down in M2.

### Levels

- Undiggable blocks look different from diggable bricks. On level 1, the left part of the top brick row is undiggable.

### Technical

- **Interrupt routine** (2026-11-14 and 2026-11-15): during the game the vector at `$0314`–`$0315` points to the game's own routine at `$648A`. It runs about 60 times per emulated second, so it's driven by the CIA timer, not by the screen. It stops for about 1.5 s while a level is set up, and once it didn't run for 1.2 s during a level. While the trainer menu waits, the vector points to the KERNAL's routine, `$EA31`. Not checked: whether it changes between levels.
- **Jiffy clock** (`$A0`–`$A2`): the KERNAL's clock keeps counting during the game, about 60 times per emulated second, except while the interrupt routine stops. Over longer stretches it's a reliable measure of emulated time.

## RAM map

Filled in during M2. Addresses are for version A.

| Address | Size | Meaning | Notes |
| --- | --- | --- | --- |
| | | | |
