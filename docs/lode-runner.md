# Lode Runner reference

What we know about the C64 Lode Runner we use: the versions on the disk, how the game behaves, and where it keeps its data in RAM. This is reference material for every milestone, so it lives outside the milestone documents.

## Versions on the disk

`C:\Emulation\ROMs\C64\Lode Runner.d64` holds **three versions** of the game (files from `c1541 -list`, load addresses from the file headers). We use **version A** (D6). Stick with it, because RAM addresses can differ between versions.

| Version | Files | What we know |
| --- | --- | --- |
| A | `loderunner+3m/pw` (63 blocks, $0801) | First file on the disk, so `LOAD"*",8,1` (or autostarting the image without a file name) loads this one. Starts with a trainer menu: `UNLIMITED LIVES (Y/N)?`, `TOTAL INVINCIBILITY (Y/N)?`, a note that the C= key skips levels during the game, then press SPACE. Its status line is clean. In memory it's the original game at the original addresses, with three changes (see Trainer below). |
| B | `loderunner i` (3 blocks, $0801) + `it` (89 blocks, $6000) + `db` (12 blocks, $1400) | Crack by "M & J", January 1984. `loderunner i` is a BASIC loader: it shows a loading screen ("plug joystick into port #2"), then a small machine-code routine (POKEd into screen memory and hidden by black-on-black colour) loads `it` and `db` and jumps to $6000. So `it` is the main program and `db` is data of some kind. |
| C | `lode runner` (73 blocks, $0801) | Tried first (2026-11-14, `-autostart "...Lode Runner.d64:lode runner"`). Plays fine, but the status line at the bottom has stray white pixels. |

### The disassembly

[Piddewitt/Loderunner](https://github.com/Piddewitt/Loderunner) on GitHub is a reverse-engineered, commented source of the C64 original that assembles into the exact original program: `asm/it.asm` (the main program, `it`, loaded at `$6000`–`$B7FF`), `inc/LR_ZPG.asm` (zero page) and `inc/LR_Var.asm` (variables and tables). The RAM map below comes from it. Some addresses in its comments don't match its own definitions (for example the hole tables), so the map uses addresses worked out from the definitions. Its files are the game's code, so they stay outside this repository.

Compared on 2026-11-15 (M2 F2), with version A in level 1: version B's `it` differs from the disassembly's `it.prg` in 4 bytes, and version A's `$6000`–`$B7FF` in 22 bytes. Apart from the trainer's changes, all of them are code that changes itself while it runs (a colour, the volume, a disk command and some pointers, between `$622F` and `$8EBE`).

### Trainer

Version A's trainer changes three things in the game's code (M2 F2, 2026-11-15), and nothing else:

- `$6008`: the call to the copy protection is `NOP`ed out (`JSR $8E11` → `EA EA EA`), always.
- `$61A2`: with Y to unlimited lives, `DEC $1312` (take a life) becomes `BIT $1312`, which does nothing. So the life counter never goes down.
- `$611F`: in the main loop, `LDA $1314` becomes `JSR $0334`, a routine of the trainer's in the cassette buffer. It does the replaced `LDA $1314`, but if C= is held (`$DC01` bit 5), it leaves the main loop and jumps to the game's own "level complete" (`$6162`). So a C= skip also gives a life and 1,500 points, like finishing the level. Seen in M2 F4: on C= the level number went up at once and the lives went 5 → 6.

Checked with Y to unlimited lives and N to invincibility (D6). Not checked: the other answers, though N to unlimited lives presumably leaves `$61A2` alone.

## Game notes

Observations about the game itself, gathered while playing and in the libretro core. Frame numbers count from the moment SPACE is pressed in the trainer menu, at 50.125 frames a second. "Not checked" marks what was inferred rather than seen.

### Start-up

- **Loading** (libretro core, 2026-11-15): the trainer menu appears 2,532 frames, about 50 emulated seconds, after loading starts.
- **Trainer menu** (libretro core, 2026-11-15): its text is in the screen memory at `$0400`: row 2 `UNLIMITED LIVES (Y/N) ?`, row 4 `TOTAL INVINCIBILITY (Y/N) ?`, row 7 `C= SKIPS LEVEL DURING GAME`, row 10 `(SPACE)`. Each answer is one key press, Y or N, and SPACE ends the menu. While the menu waits, the interrupt vector at `$0314`–`$0315` points to the KERNAL's own routine, `$EA31`; right after SPACE it points to the game's, `$648A`.
- **Title screen and demo** (2026-11-15): after the menu the game shows its title screen, then plays itself (the demo). The demo is running by about frame 1000 (20 s). Pressing SPACE or fire during the demo starts a new game, but only once the demo is running: it first spends about 500 frames setting up its level, and ignores both meanwhile (2026-11-16: fire at the demo's first frame did nothing; fire once the demo ran started a new game 2 frames later). The interrupt vector points to `$648A` from the title screen on, so it doesn't tell the title, the demo and the game apart.
- **Invincibility breaks the demo** (libretro core, 2026-11-15): with the trainer's invincibility on, the demo player gets stuck after about 5 seconds (at the bottom of the left ladder of the demo level, with a guard on top of him), and from then on SPACE no longer starts the game. With invincibility off, the demo keeps playing and SPACE works. Unlimited lives doesn't break it. The demo replays a fixed list of moves, each a direction or fire and how long to hold it (`DemoHandler` and `TabDemoMoves` in the disassembly), so presumably a player who can't be caught falls out of step with it.
- **Level start times** (libretro core, M2 F4, 2026-11-15, level 1, a joystick direction held until the level runs): from SPACE in the demo until the level runs, 644 frames (about 13 s), while the level is drawn bit by bit; from loading the disk to there, flat out, about 4,170 frames, 6.3 s (F5). After a death, 11–12 frames of dying, then 885 frames (about 18 s) until the level runs again: it's drawn again from scratch. A player standing still on level 1 was caught about 247 frames (5 s) after the level started running, every time. After the last death, the title screen comes back 834 frames (about 17 s) later.
- **What starts a waiting level** (from the disassembly, `PlayerCheckInput`; seen in M2 F4 and F5): once a level is set up in a real game (not the demo), the player flickers and the main loop waits until a joystick direction or fire is pressed, or a key is waiting in `$1328`. The game reads that only once the level is ready, and RAM doesn't show when that is, so a short tap can come too early and be lost; a direction held until the level runs always worked. The SPACE that starts a new game from the demo is still waiting in `$1328` (`$3C`), so a new game's first level started with SPACE starts by itself as soon as it's ready. Fire leaves nothing waiting (`$00`), so a game started with fire waits for the first move, as after a death. `GameStarter` uses fire.
- **Picking the level** (M2 F5, D13): in the few frames between SPACE or fire in the demo and the new game loading its level (play mode, player still dead from the demo), writing a level number into `$1310` (0–149) and `$1305` (1–150) makes the game load that level. Levels 1, 5 and 150 came up with 5 lives and a score of 0, about 640 frames later. `GameStarter.StartLevel` does this from a saved state, so like every load it starts with no input (D15): the first move is pressed after it.

### Controls

- **Joystick in port 2**, as version B's loading screen says. Confirmed for version A by the owner, and by the player walking right with the joystick in port 2 in the libretro core (2026-11-15).
- **The joystick as the C64 sees it** (`$DC00`, read in the libretro core, 2026-11-15): nothing `$7F`, right `$77`, left `$7B`, up `$7E`, fire `$6F`. A pressed direction or fire pulls its bit low: bit 0 up, bit 2 left, bit 3 right, bit 4 fire, and bit 1 down (not measured; the standard C64 layout).
- **SPACE or fire** starts the game from the demo, once it runs (see Start-up).
- **C= skips to the next level** during the game, as the trainer menu says. In our window that's Left Ctrl, which the libretro core maps to C= (tried by the owner, 2026-11-15). It goes through the game's own "level complete", so each skip adds a life and 1,500 points (M2 F10). Not used (D6): `GameStarter` starts a new game at any level instead (D13).
- **Digging** (M2 F7–F10): **fire** digs the brick diagonally below the player, on the side of his **last left or right move**, not the way his picture faces. Fire takes priority over the joystick's directions in the same pass, so to dig on the other side, first make a step that way, then fire. Ctrl+D swaps the side (dig mode `$1339`); we leave it alone. The dig turns him to face the side he digs, takes about 20 frames, and only then becomes a hole. Digging works while running too (owner, 2026-11-15). Seen: no move yet at the start of level 1 → left; after a step right → right.

### Play

- **Moving** (M2 F8): the player moves in steps of a fifth of a tile, both across and up and down; his position within the tile goes from 0 to 4, with 2 centred.
- **Guards and holes** (M2 F9, level 1): a guard who falls into a hole scores 75. His hole timer starts at the level's hole time (`$26`, 38 passes, on level 1) and counts down before he climbs out; if the hole closes first, he dies (75 more) and comes back at his respawn place 20 passes later. Seen: a hole dug at the start of level 1 closed on a guard 74 frames after he fell in. A guard in a hole can be walked over.
- **After a death** (M2 F10): the level starts over with all its gold back; the score stays.

### Levels

- Undiggable blocks look different from diggable bricks. On level 1, the left part of the top brick row is undiggable.
- **Hidden exit ladders** (M2 F7, all 150 levels): 39 levels have none, so their way out is there from the start. The others have up to 45, the most the game keeps (it blanks out any more). Level 1's are in column 18, rows 0–3.

### Technical

- **Interrupt routine** (2026-11-14 and 2026-11-15): during the game the vector at `$0314`–`$0315` points to the game's own routine at `$648A`. It runs about 60 times per emulated second, so it's driven by the CIA timer, not by the screen. It stops for about 1.5 s while a level is set up, and once it didn't run for 1.2 s during a level. While the trainer menu waits, the vector points to the KERNAL's routine, `$EA31`. It stays `$648A` between levels, after deaths and through game over: `GamePhaseReader` relies on that, and its phases stayed right through all of those in M2 F4–F10.
- **Main loop** (from the disassembly, M2 F2): the interrupt routine only plays sound and reads the keyboard. Everything else happens in the main loop at `$611C`: one pass moves the player, closes holes and respawns guards, moves the guards, and then waits at `$6152` until the interrupt counter (`$132D`) reaches the game speed (`$131B`). At normal speed that's a pass every 2 interrupts, about 30 a second, against 50 frames a second. A hole timer counting down 31 in 50 frames agrees (F1).
- **A pass takes more than half its time** (M2 F6, measured with the CPU's PC at the end of each frame): 55–62% of frame ends fall inside a pass, with 3 to 5 guards, most of it drawing (code at `$80xx`–`$8Dxx`). So RAM read after a frame is usually taken halfway through a pass. What's half done then is small: the guard being moved has his new position only in the zero-page copy and maybe the grid, while his table entry still has the last one (11–15% of frames), and about 1 frame in 3,000 has the player's tile missing from the grid.
- **A RAM compare during play** (M2 F1, in the demo, one second apart) also shows changes outside the game's data: the stack (`$01E8`–`$01F8`), sprite data at `$0C00`, parts of both hires screens, the sound buffer at `$C00C`, code that changes itself, and many zero-page bytes besides the player's.
- **Jiffy clock** (`$A0`–`$A2`): the KERNAL's clock keeps counting during the game, about 60 times per emulated second, except while the interrupt routine stops. Over longer stretches it's a reliable measure of emulated time.

## Reading RAM

RAM is read after every frame, and most frames end halfway through a pass of the game's main loop (M2 F6). So each thing is read from where it's always complete (D14):

- **The level:** the grid as built (`$0A00`), not as played (`$0800`), whose player and guard tiles can be halfway through a move. It includes the gold still lying around and, once all gold is taken, the exit ladders. Holes aren't in it: they come from the hole tables (`$12A0`–`$12FF`), whose entries are written in one go when a dig finishes.
- **The guards:** their tables (`$1260`–`$129F`), not the grid's guard tiles or the zero-page copy (`$15`–`$1B`). The game writes a guard's table entry in one go after his move, so every entry is complete; the guard being moved is one move (at most 33 ms) behind.
- **The player:** `$03`–`$07`. About 1 frame in 3,000 to 10,000 catches his move halfway; then the grid as played has no player tile at his column and row, or his step is out of range (`$FF` for a moment), and the frame is skipped (`GameState.TryRead` gives false).

## RAM map

Addresses are for version A, from the disassembly (see above) unless the notes say otherwise. **Confirmed** means seen in the running game, with the step that saw it; everything else is still the disassembly's word. Columns are 0–27 from the left, rows 0–15 from the top. "Pass" means one pass of the game's main loop (see Technical), about 30 a second.

### Level grid

| Address | Size | Meaning | Notes |
| --- | --- | --- | --- |
| `$0800`–`$09C3` | 16 × 28 | The level as played: one byte per tile, row by row. Row r starts at `$0800 + 28 × r` for rows 0–8, and 4 bytes later for rows 9–15 (row 9 at `$0900`), so no row crosses a page. Holes (as empty tiles), the player (9) and the guards (8) are marked in it. Only digging, closing holes, the exit ladders and the figures' moves write it. | Confirmed (F1, F7): guard and player codes moving; a taken gold went `07` → `00` at row 14, column 7 (`$0993`); on all 150 levels it differs from the level as built only at the player and guards when the level starts. |
| `$0A00`–`$0BC3` | 16 × 28 | The level as built, laid out the same way, without the player, guards or holes: a dug brick stays a brick. Gold leaves it when taken or picked up by a guard and comes back when dropped; the exit ladders join it as ladders (3) once all gold is taken. Respawning guards look for an empty tile in this one. | Confirmed (F1, F7): the same gold went `07` → `00` here too (`$0B93`); on all 150 levels it holds only codes 0–5 and 7, and its gold adds up to `$130E`. |

Tile codes: 0 empty, 1 brick (diggable), 2 solid block, 3 ladder, 4 rope, 5 trap brick (looks like brick, falls through), 6 hidden exit ladder (blanked when the level starts and listed in the exit ladder table), 7 gold, 8 guard, 9 player.

### Player

| Address | Size | Meaning | Notes |
| --- | --- | --- | --- |
| `$03` | 1 | Column | Confirmed (F8): against the screen. |
| `$04` | 1 | Row. The level is complete when this is 0, `$06` is 2 and no gold is left. | Confirmed (F8): against the screen. |
| `$05` | 1 | Position within the tile, left to right: 0–4, 2 = centred. Each step of a move changes it by 1; at 4 the column goes up and it starts again at 0 (and the other way round). | Confirmed (F8): walking right went 2, 3, 4, then the next column at 0. For a moment it can be `$FF` while the game moves on to the next tile. |
| `$06` | 1 | Position within the tile, top to bottom: 0–4, 2 = centred, stepping like `$05` | Confirmed (F8): climbing went 2, 1, 0, then the row above at 4. |
| `$07` | 1 | Sprite: `$00`–`$02` run left, `$03`–`$05` rope left, `$06` dig left, `$07` fall left, `$08`–`$0F` the same facing right, `$10`–`$11` ladder. It's the picture shown, not the movement: it stays until the next move. | Confirmed (F8): ladder, rope left, fall left, dig left, run right, each against the screen. |
| `$1314` | 1 | Alive: 1 alive, 0 dead (caught, trapped in a hole, or the demo ended) | Confirmed (F4): 0 from each death until the level is set up again, and from the end of the demo until the new game's level is set up. |
| `$1315` | 1 | Falling: `$00` falling, `$20` not, `$FF` at the start | Confirmed (F8): `$00` while dropping from a rope, not after landing. |
| `$1316` | 1 | Digging: `$00` no, `$01` right, `$FF` left | Confirmed (F8): `$FF` while digging to the left. |

### Guards

Up to 5 guards, numbered 1–5; entry 0 of each table is unused. The main loop moves one guard at a time through a working copy in the zero page, so while a guard is being moved, its table entries are out of date.

| Address | Size | Meaning | Notes |
| --- | --- | --- | --- |
| `$131C` | 1 | Number of guards (0–5) | Confirmed (F1): 3 in the demo. |
| `$1260`+n | 1 each | Column of guard n; while he waits to respawn, the column he'll appear in | Confirmed (F1, F9): against the screen. |
| `$1268`+n | 1 each | Row | Confirmed (F1, F9): against the screen. |
| `$1270`+n | 1 each | Gold or hole timer. 0: free. Negative: he carries gold, counts up, and drops it at 0. Positive: he's stuck in a hole, counts down from the level's hole time (`$1320`); he shakes below `$0D` and climbs out below `$07`. `$7F` while he waits to respawn. | Confirmed (F9): `$F5` after picking up gold, `$26` on falling into a hole, `$7F` after dying. |
| `$1278`+n | 1 each | Position within the tile, left to right (0–4, 2 = centred) | Changing (F1). Read by `Guard` as an offset; not compared with the screen one by one. |
| `$1280`+n | 1 each | Position within the tile, top to bottom (0–4) | Confirmed (F11): 0 (offset −2) for a guard at the top of a ladder tile, against the screen. |
| `$1288`+n | 1 each | Sprite: `$00`–`$02` run left, `$03`–`$05` rope left, `$06` fall left, `$07`–`$0D` the same facing right, `$0E`–`$0F` ladder (no dig picture, unlike the player's) | Confirmed (F11): ladder, against the screen. Rope and fall not seen for a guard. |
| `$1290`+n | 1 each | Facing: `$FF` left, `$01` right | Confirmed (F1): `FF` → `01` as a guard turned. |
| `$1298`+n | 1 each | Respawn timer: 0 while he's in play; starts at `$14` (20 passes) when a guard dies; he reappears in two steps (at `$13` and `$0A`) and moves again at 0 | Confirmed (F9): 20 when the hole closed on him; he appeared 33 frames (20 passes) later. |
| `$131E` | 1 | Respawn column: steps by one each pass, 0–27. A dead guard comes back on row 1 at the first empty tile (in the level as built) from this column on, trying the next rows if a row has none. So respawning isn't random. | Confirmed (F1, F9): one step per pass; a guard who died on level 1 came back at (26,1), an empty tile on row 1. |
| `$131F` | 1 | The guard being moved (1–5) | Confirmed (F6): his working copy differed from his table entry in 11–15% of frames, as a move in progress would. Not read by the bot (D14). |
| `$15`–`$1B` | 7 | Working copy of the guard being moved: column, row, sprite, facing, gold or hole timer, position within the tile (left to right, top to bottom) | Confirmed (F6), as for `$131F`. Not read by the bot (D14). |
| `$1320` | 1 | How long guards stay in a hole; set when a level starts, from a table indexed by `$1311` (0 in a new game) | Confirmed (F9, F11): `$26` on level 1, the value a trapped guard's timer started at. |

### Holes and exit ladders

| Address | Size | Meaning | Notes |
| --- | --- | --- | --- |
| `$12A0`+i | 30 | Column of the hole in slot i (0–29); only meaningful while its timer isn't 0 | Confirmed (F10): (13,15) and (15,15) as dug. |
| `$12C0`+i | 30 | Row of the hole in slot i | Confirmed (F10). |
| `$12E0`+i | 30 | Timer of the hole in slot i, 0 = free slot. Set to `$B4` (180) when a dig finishes, together with the empty tile in the level as played; one less each pass. The picture shows the hole filling in at `$14` and `$0A`, but the tile stays empty until 0, when the brick comes back and anyone inside dies. A dig needs a free slot among the first 30. | Confirmed (F1, F10): `8F` → `70` in 50 frames; a hole lasted 299 frames (179 passes) from appearing to closing, and the brick came back in the level as played. |
| `$1301` | 1 | Number of hidden exit ladders, at most 45 | Confirmed (F7): 0–45 over the 150 levels, 4 on level 1. |
| `$1201`+n, `$1231`+n | 1 each | Column and row of hidden exit ladder n (from 1) | Confirmed (F7): level 1's are column 18, rows 0–3; on every level they're on empty tiles. |

### Game

| Address | Size | Meaning | Notes |
| --- | --- | --- | --- |
| `$1305` | 1 | Level number, 1–150. Goes up at the start of the game's "level complete", before the score is counted up, and with a level skip. | Confirmed (F2, F5, F10): 1 in level 1, picked with `GameStarter`, 2 at once after C=. |
| `$1306` | 1 | Game mode: 0 title screen, 1 demo, 2 play (also while the game-over sign and the high-score list show), 3 play from the level editor, 5 level editor | Confirmed (F1, F2, F4): 0 on the title screen, 1 in the demo, 2 from SPACE until the title screen comes back after game over. Leftovers before the game runs (3 was seen during loading). |
| `$130A`–`$130D` | 4 | Score, BCD (two digits per byte), lowest byte first | Confirmed (F1, F9, F10): 250 after a gold, 75 for a guard in a hole and 75 more when it closes on him, 1,500 for a level, counted up 100 at a time. |
| `$130E` | 1 | Gold left in the level, including gold carried by guards; `$FF` once the exit ladders have been shown. While a level is set up, it's set to 0 and counted up again. | Confirmed (F1, F4, F7, F10): one less after a gold; back to the level's full count after a death; `$FF` after the exit ladders appeared; 0 for a while during set-up, which is why "no gold left" can't tell a completed level. |
| `$1310` | 1 | Level number as stored on disk, 0–149. When a level starts, the game loads this one from disk (unless it's the one already loaded). | Confirmed (F5): written before a new game loads its level, it picks the level. |
| `$1312` | 1 | Lives; 5 at the start, one more per level completed | Confirmed (F2, F4, F10): 5 in level 1, one less per death with N to unlimited lives, 0 at the last death, one more at once on C=. |
| `$1318` | 1 | Cheated (no high score): `$00` yes, `$01` no | Seen (F11): `$01` in a new game. Not used by the bot. |
| `$1319` | 1 | Joystick up/down as the game read it: `$00` none, `$21` up, `$25` down; `$1E` and `$26` when digging left and right | Seen (F11): `$21` holding up on a ladder; but `$FF` in both joystick bytes while holding fire on a ladder, not `$1E`/`$26`. Not used by the bot. |
| `$131A` | 1 | Joystick left/right: `$00` none, `$22` left, `$2A` right; `$1E` and `$26` when digging | Confirmed (F1, F11): `22` → `2A` as the demo turned right; `$22` holding left. Not used by the bot. |
| `$131B` | 1 | Game speed: the main loop waits until the interrupt counter reaches it, so 5 (normal) means a pass every 2 interrupts. 3–8. | Seen (F11): 5. The ~30 passes a second measured in F1 and F10 fit it. |
| `$1328` | 1 | The last key pressed, as the interrupt routine stores it (0 = none). Read and cleared by the game's key commands; a waiting level starts when it's not 0. | Seen (F5, 2026-11-16): the SPACE that starts a game is still here (`$3C`) and starts its first level; fire leaves it `$00`, and the level waits for the first move. |
| `$132A` | 1 | Death tune: not 0 while it plays after the player dies | Not checked. Not used by the bot: `GamePhase` tells dying from `$1314`. |
| `$132D` | 1 | Interrupt counter: counted up by every interrupt, set to 3 after each pass. The main loop waits until it reaches the game speed (`$131B`). Nothing else sets it back, so it only counts up while the main loop isn't running. | Confirmed (F4): read this way, "main loop running" matched every stretch of play to game over. |
| `$1339` | 1 | Dig side: `$00` behind the player, `$FF` in front (Ctrl+D toggles it) | Seen (F11): `$FF` in a new game; the digging side in F10 matched it. |
| `$08` | 1 | Set by the player's last left or right move: `$FF` left, `$01` right. Fire digs left when this XOR `$1339` is positive, otherwise right. | Seen (F10): `$01` after a step right, and he dug right. |

### Code addresses

| Address | What | Notes |
| --- | --- | --- |
| `$611C` | Main loop: move the player, close holes, move guards | |
| `$6152`–`$6161` | The main loop's wait for the interrupt counter, then back to `$611C` | Seen (F6, F10): the CPU's PC at `$6152`, `$6155` and `$6158` at the end of many frames. |
| `$6162` | Level complete | Seen (F2): where the trainer's C= hook jumps; C= completes the level (F4, F10). |
| `$648A` | Interrupt routine: sound and keyboard only | Seen (M1, F2): the interrupt vector points here from the title screen on. |

The bot's code names the addresses it reads in `RamMap` (`C64Bot.LodeRunner`), with the same meanings.
