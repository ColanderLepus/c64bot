# M1: Connect C# to the emulator

**Status:** Done, 2026-11-15 (part 1 replaced by part 2; part 2: T1–T8, T10, R1 and R3 done; R2 waiting; T9 moved to M4; T11, sound, left for later)

Make a C64 emulator controllable from C#: read memory, send input, control the timing and show the picture. The bot plays at real-time speed (D4), so this has to work about 50 times a second without changing the game's speed.

M1 has two parts:

- **Part 1** (S1–S6, 2026-11-14) built a client for the VICE program's binary monitor. It worked, but S6 showed that talking to a running VICE upsets its timing, so real-time play needed workarounds. It's summed up at the end of this document.
- **Part 2** (from 2026-11-15) runs the emulator inside our program as a libretro core instead, using VICE's C64 emulator as the core. T1–T6 were a trial on the branch `try-libretro-core`; it worked, D2, D4 and D8 were changed to it, and the branch was merged into `main`. The steps go on from T7.

## Tasks

- [x] Load the VICE core and run Lode Runner (T2, T3)
- [x] Read RAM (T3)
- [x] Keyboard input (T4)
- [x] Real-time loop at the real C64's speed (T5)
- [x] Show the picture (T6)
- [x] Joystick input (T7)
- [x] Tidy up and test the frontend (R1, R3)
- [x] Save and load snapshots (T8)
- [x] Keyboard and joystick from the window, so a human can play (T10)
- Sound (optional): left for later, see T11

## Part 2: the VICE libretro core

A **libretro core** is an emulator built as a DLL with a plain C interface (`libretro.h`). A host program, the **frontend**, loads it and calls `retro_run` once per frame. Our frontend is `LibretroCore` in `C64Bot.Libretro`; names follow `libretro.h`.

How the frontend sets up the core:

1. Load the DLL and look up all its libretro functions once. Every core has all of them, so a missing one means the DLL isn't a core.
2. Give the core its callbacks: environment (the core's questions to the frontend), video refresh, audio, input poll and input state.
3. Set any core options that differ from their defaults (the core announces them in `retro_set_environment`).
4. `retro_init`, then `retro_load_game` with the disk's path, then `retro_get_system_av_info`, then `retro_set_controller_port_device` to plug a RetroPad into the first port.
5. `retro_run` once per frame. `retro_get_memory_data` gives a pointer to the C64's 64 KB RAM.

`LibretroCore` checks that the steps come in this order, and throws if they don't, instead of letting the core crash.

Environment commands the frontend answers (numbers as in `libretro.h`):

| Command | Answer |
| --- | --- |
| 9 `GET_SYSTEM_DIRECTORY`, 31 `GET_SAVE_DIRECTORY` | The folders given to `Initialize` (the app's are `system\` and `saves\` next to the DLL) |
| 10 `SET_PIXEL_FORMAT` | Yes for XRGB8888 only |
| 12 `SET_KEYBOARD_CALLBACK` | Keeps the core's function for key presses |
| 15 `GET_VARIABLE`, 16 `SET_VARIABLES` | Core options: stores the defaults the core announces, answers with the current values |
| 27 `GET_LOG_INTERFACE` | Our log function |

Everything else gets "no", and the core copes. Without 27, 15/16 and the call to `retro_get_system_av_info`, it crashes (see Notes).

### Steps (part 2)

Done one step at a time, checking each before starting the next. T1–T6 were the trial; each step needs the core DLL (see Environment in `PLAN.md`), but not the VICE program.

- [x] **T1. Get the core.** The prebuilt Windows DLL from the libretro build server, in `C:\Tools\C64\libretro`.
- [x] **T2. Talk to it.** Load the DLL and call `retro_api_version` and `retro_get_system_info`. Check: the app printed API version 1, "VICE x64sc 3.10 76cde0b", that the core wants a file path, and that `d64` is among the files it loads.
- [x] **T3. Run Lode Runner without a window.** Set up the core, load the disk, run 3,000 frames and read RAM. Check: the trainer menu's text was in the screen memory at `$0400`. The 60 emulated seconds took 4.6 s.
- [x] **T4. Keyboard.** Press and release keys through the core's keyboard callback. Check: Y, Y and SPACE went through the trainer menu, and the game's own interrupt routine at `$648A` took over, as in the VICE program.
- [x] **T5. A steady clock.** A frame clock (`FrameClock`) that sleeps on a high-resolution Windows timer and spins only for the last millisecond. Check: the jiffy clock showed 100.0% speed over 10 seconds.
- [x] **T6. The picture in a window.** A WPF window (D11) on its own thread shows the newest picture from the core; the emulation runs on the frame clock until the window closes. Check: Lode Runner ran in our window at normal speed and looked smooth.
- [x] **T7. Joystick.** Answer the core's input state questions for the RetroPad (libretro's standard gamepad), whose D-pad and B button the core turns into the C64 joystick and fire button. The core option `vice_joyport` chooses the C64 port; it's set to 2. Check: the C64 read the right bits at `$DC00`, and with key presses and moves at fixed frames (removed again in T8), the player walked right along the bottom row once the level was ready.
- [x] **R1. Tidy up `LibretroCore`** (after a review of all code and documents, 2026-11-15). The core's functions are looked up once at load time instead of on every call; a DLL that isn't a core is rejected at once. The class checks that its steps come in the right order, and `Dispose` can safely be called twice. No exception can escape a callback into the core. Option defaults are parsed safely. The members are in the order they're used, split over three files: the public API, the callbacks, and the C side (functions and structs). Thread safety is documented: one thread only. `C64Bot.Libretro` targets `net10.0-windows`, because `FrameClock` needs Windows; the clock now also reports a failed wait and can be disposed twice.
- [ ] **R2. Split up `Program.cs`.** Not done: the app has no real role yet beyond showing what happens, so it stays as it is for now (owner's call, 2026-11-15). Still waiting after the review of 2026-11-16 (owner's call).
- [x] **R3. Tests for the frontend.** `C64Bot.Libretro.Tests`: option default parsing, loading a missing file or a DLL that isn't a core, the order checks, `Dispose` twice, the frame clock's timing, and tests on the real core, which run Lode Runner to the trainer menu once and share it: RAM, options, the picture's size, and a key press answering the menu. The tests that need the core DLL or the disk are skipped on a PC without them. 23 tests, about 4 seconds.
- [x] **T8. Snapshots.** `LibretroCore.SaveState` and `LoadState` save and load the whole machine in memory, through `retro_serialize_size`, `retro_serialize` and `retro_unserialize`. A save state is libretro's word for what VICE calls a snapshot. In the app's console, T saves a state and L loads it; the fixed key presses from T7 are gone from the app, so the demo just runs. Tests: loading a state brings back all 64 KB of RAM exactly, and an empty or broken state is refused. Check: save a state while the demo plays, let it play on, load the state, and the demo jumps back to that moment. It did (2026-11-15). Two runs from the same state with the same key presses don't repeat exactly, because of VICE's key delay (see Notes).
- **T9. Debug mode.** Moved to M4 (owner's call, 2026-11-15). Stepping through frames is for debugging the bot, together with the debug view; until the bot exists, VICE's monitor is the better tool for looking at the game.
- [x] **T10. Play in the window.** The window's keyboard goes to the C64's keyboard, and some keys work as the joystick. The window can't use the core itself (it's on another thread), so it puts presses and releases in an `InputQueue`, which the emulation loop applies before each frame. `KeyMap` decides what each PC key does: the arrow keys and Right Ctrl are the joystick (directions and fire) and go nowhere else, and letters, digits, Space, Return, Shift, Left Ctrl, F1–F8 and so on go to the C64's keyboard through the core's own key map. Key repeat is ignored, and everything is released when the window loses the focus. The console keeps T and L for save states; its other keys are gone. Check: the owner played level 1 in our window (2026-11-15).
- **T11. Sound (optional).** Play the core's sound samples on the PC. Needs an audio library; choose it in this step. Left for later (owner's call, 2026-11-15): the bot doesn't need sound, so it can be done whenever it's wanted.

### Notes (part 2)

- **The core in a frontend of our own** (seen in T3, 2026-11-15):
  - Without a log interface (command 27), the core falls back to its own logger, which writes to stderr and crashes in our process with an access violation.
  - Every frontend calls `retro_get_system_av_info` after loading a game, and the core sets up its frame rate there. Without it, the first `retro_run` divides by zero.
  - Without answers to the core options (commands 16 and 15), the sample rate is 0, and `retro_run` divides by zero again.
  - Our log function only passes on the format string, without its values (`%s`, `%d` stay as they are). Enough to see what the core is doing; printing the values would need a C helper.
- **Facts about the core** (2026-11-15):
  - 50.125 frames a second (PAL), sound at 48,000 Hz, pictures of 384×272.
  - The picture comes in 16-bit colour by default. The core option `vice_gfx_colors` = `24bit` switches to XRGB8888, which in memory is what WPF calls `Bgr32` (blue, green, red, unused).
  - Key presses go to the C64's keyboard matrix at the next frame, like real keys, not into the keyboard buffer. The core ignores the character and the modifiers in the event.
  - From loading the disk to the trainer menu takes 2,532 frames, about 50 emulated seconds. Flat out, that's a few seconds.
  - `SystemRam` is plain RAM only. Where the C64 shows ROM or I/O chips instead, it holds the RAM underneath, so for example the joystick port at `$DC00` can't be read through it (T7 needed a routine running on the C64 for that).
- **Half-updated state.** RAM is read at the end of a frame, which often falls in the middle of the game's update. M2 F6 measured how often and what's half done, and D14 says how the bot reads RAM anyway.
- **Joystick, seen in T7** (2026-11-15):
  - The core keeps a "device plugged in" per RetroPad port, starting with none, and ignores the buttons of an empty port. RetroArch plugs a RetroPad in after loading a game with `retro_set_controller_port_device`; our frontend must do the same.
  - The core asks for each RetroPad button separately, every frame, on ports 0–5 (we declined the bitmask that answers them all at once).
  - The C64 reads the joystick at `$DC00` as it should (values in `lode-runner.md`). `SystemRam` can't show I/O chips, so a small routine on the C64 copied `$DC00` into RAM on every interrupt to check it.
  - Fire at frame 1000 didn't end the demo, because the demo was still setting up its level; like SPACE, fire works once the demo runs (settled 2026-11-16, see `lode-runner.md`).
- **Which runs repeat exactly** (2026-11-15 and 2026-11-16):
  - Runs with key presses don't. VICE hands a key press to the C64 after 1,000 cycles plus a random 1 to 19,656 cycles (up to a frame), to imitate a person's uneven timing. Its random number generator isn't in the save state, and no core option turns the delay off (VICE's `keyboard.c` and `lib.c`). So two runs from the same state with the same key presses differ from the second frame on. That's presumably also why two runs of T7 differed by 63 bytes of RAM at frame 1300.
  - The joystick has no random delay in the libretro build: VICE's comment there says "Random delay removed", and it uses a fixed quarter frame (`joystick.c`, read 2026-11-16).
  - Loading a level from disk isn't repeatable either: two runs from the same new game (`GameStarter.StartLevel`) with the same joystick input first differed 94 frames in, in the KERNAL's serial bus bytes (`$95`, `$A5`) while the level loaded, and later in the guards' tables. The cause wasn't checked; VICE's drive emulation varying the disk's speed at random is a likely one. Without the disk, level starts do repeat: a level put straight into the game's level buffer is set up without reading the disk, and two runs from `StartLevel` that way had the same RAM after each of 2,500 frames (2026-11-16, see "Levels on the disk" in `lode-runner.md`).
  - From a state saved once the level runs, joystick-only runs repeat byte for byte: two runs of 3,000 frames each on levels 1 and 5, deaths included, had the same RAM after every frame (2026-11-16). After a death the game sets the level up again from memory, without the disk.
  - The bot doesn't need exact replays, because it reacts to the state after every frame (D4). But a bot run flat out from the same state repeats exactly too, which makes the bot easy to test.
- **Save states, seen in T8** (2026-11-15):
  - A state is about 190 KB (193,849 bytes at the trainer menu) and holds 30 parts: CPU, RAM, CIAs, SID, VIC-II, the disk drive, keyboard, joysticks and so on. The disk's contents aren't in it, only its file name, so a state needs the same disk inserted.
  - Before saving or loading, the core lets the emulated CPU finish its current instruction. `retro_serialize_size` measures the size by saving a whole state, so `SaveState` asks every time.
  - Loading a state is complete: load, save, load the same state, save again, and the two saved states are identical byte for byte. Runs from a state still differ when they use keys (see "Which runs repeat exactly" above).
- **Input across a load** (read in the core's source at build `76cde0b`, `libretro/vice-libretro` on GitHub, 2026-11-16; found when a held move didn't start a level after `StartLevel`):
  - The libretro port asks about the RetroPad at the start of every frame and hands VICE the whole joystick value. Keys come in as events; each frame the port hands VICE the keys that changed, in key-code order, not in the order they came.
  - VICE keeps the last joystick value and the last keys it was given (its latches, `latch_joystick_value` and `latch_keyarr`) and only acts on a change: then, after a delay (a quarter frame for the joystick, up to two frames for keys), it copies the latch to what the C64 reads. A state saves only what the C64 reads, not the latches, and loading one doesn't reset them; neither are the port's own memory of the last keys, or VICE's time stamp for the next key event.
  - VICE's joystick snapshot has an off-by-one: port n's value is saved from slot n but loaded into slot n + 1. So after a load our port 2 reads port 1's saved value, which is always at rest. (A one-character bug in VICE's `joystick.c`, not reported.)
  - The core's reset clears the keyboard and joysticks ("Release keyboard keys and joystick switches"); loading a state doesn't.
  - So a button held across a load never arrived (the same value as before isn't a change), and a Shift let go of at a load could still be down when a key pressed right after it arrived, which made that key a shifted one. D15 makes every load start with no input at all: `LoadState` lets go of everything, and the first frame after it runs with no input.
- **Measurements from T5** (2026-11-15, during a level, 10 seconds): 100.0% speed by the jiffy clock; frames 19.927–19.974 ms apart (19.950 ms on average); emulating a frame took 1.48 ms on average, 2.10 ms at worst; the program used 11% of one CPU core. The first attempt showed 84.7%, because measuring started right at the level start, when the game stops its interrupt routine for about 1.5 s and the jiffy clock stands still.

## Part 1: the VICE program's binary monitor

Part 1 (S1–S6, 2026-11-14) built a client for the VICE program's binary monitor: a protocol over TCP through which another program can read and write the emulator's memory and stop and resume it (chapter 13 of the VICE manual). The client worked, but real-time play didn't:

- **Short visits made the game run fast.** Reading memory from a running VICE and letting it go on, 50 times a second, made the game run at 168% by the KERNAL's jiffy clock, while VICE's status bar showed 100%. Each visit added roughly 10 ms of emulated time, whatever was read.
- **The cause is in VICE 3.10's source, and no setting changes it.** VICE takes monitor commands only at the end of a frame, and entering or leaving the monitor resets its speed regulation and suspends its sound, so every visit gained time: with the Windows sound device VICE ran flat out to refill its sound buffer, and without sound it still gained a little. The monitor is built as a debugger, for pauses of human length.
- **Lockstep worked, but only with workarounds.** A checkpoint on the game's interrupt routine stopped VICE 60 times a second, and the client held each resume back until real time had caught up, spinning on a stopwatch. That gave 100% speed.

So the emulator moved into our program as a libretro core (D2, part 2). S7–S11 were dropped, and their jobs moved to part 2: the joystick to T7, the keyboard to T4, snapshots to T8, debug mode to T9 and the screen grab to T6. The client, `C64Bot.ViceMonitor`, was removed on 2026-11-16, unused after M2 (D8). It, and this section's full version with the protocol, steps S1–S6 and all measurements, are in Git history.
