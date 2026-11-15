# M1: Connect C# to the emulator

**Status:** In progress (part 1 done and replaced; part 2: T1–T6 done)

Make a C64 emulator controllable from C#: read memory, send input, control the timing and show the picture. The bot plays at real-time speed (D4), so this has to work about 50 times a second without changing the game's speed.

M1 has two parts:

- **Part 1** (S1–S6, 2026-11-14) built a client for the VICE program's binary monitor. It works, but S6 showed that talking to a running VICE upsets its timing, so real-time play needed workarounds.
- **Part 2** (T1–T6, 2026-11-15, first as a trial on the branch `try-libretro-core`) runs the emulator inside our program as a libretro core instead, using VICE's C64 emulator as the core. D2, D4 and D8 were changed to this.

## Tasks

- [x] Load the VICE core and run Lode Runner (T2, T3)
- [x] Read RAM (T3)
- [x] Keyboard input (T4)
- [x] Real-time loop at the real C64's speed (T5)
- [x] Show the picture (T6)
- [ ] Joystick input
- [ ] Save and load snapshots
- [ ] Debug mode: one frame per key press
- [ ] Keyboard and joystick from the window, so a human can play
- [ ] Sound (optional)

## Part 2: the VICE libretro core

A **libretro core** is an emulator built as a DLL with a plain C interface (`libretro.h`). A host program, the **frontend**, loads it and calls `retro_run` once per frame. Our frontend is `LibretroCore` in `C64Bot.Libretro`; names follow `libretro.h`.

How the frontend sets up the core:

1. Give the core its callbacks: environment (the core's questions to the frontend), video refresh, audio, input poll and input state.
2. Set any core options that differ from their defaults (the core announces them in `retro_set_environment`).
3. `retro_init`, then `retro_load_game` with the disk's path, then `retro_get_system_av_info`.
4. `retro_run` once per frame. `retro_get_memory_data` gives a pointer to the C64's 64 KB RAM.

Environment commands the frontend answers (numbers as in `libretro.h`):

| Command | Answer |
| --- | --- |
| 9 `GET_SYSTEM_DIRECTORY`, 31 `GET_SAVE_DIRECTORY` | `system\` and `saves\` next to the DLL |
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
- [ ] **T7. Joystick.** Answer the core's input state questions for the RetroPad (libretro's standard gamepad), whose D-pad and B button the core turns into the C64 joystick and fire button. Find out which core option chooses the C64 port, and set port 2. Check: the app holds right for one second, then lets go, and the player walks right and stops.
- [ ] **T8. Snapshots.** `retro_serialize_size`, `retro_serialize` and `retro_unserialize`, in memory. Check: take a snapshot once the level has started, move the player, load the snapshot, and the player is back where it was. Then decide the open question about the trainer menu: every run could start from a snapshot taken after it.
- [ ] **T9. Debug mode.** The T6 loop with a different rule: one frame per key press in the window, and a key to switch between that and real time. Check: each press advances the game by one frame, and holding the key runs it in slow motion.
- [ ] **T10. Play in the window.** The window's keyboard goes to the C64's keyboard, and some keys work as the joystick. Check: you can play level 1 in our window. M3 needs this to record a human playing.
- [ ] **T11. Sound (optional).** Play the core's sound samples on the PC. Needs an audio library; choose it in this step.

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
  - From loading the disk to the trainer menu takes 2,532 frames (the disk drive is emulated accurately). Flat out, that's a few seconds.
- **Measurements from T5** (2026-11-15, during a level, 10 seconds): 100.0% speed by the jiffy clock; frames 19.927–19.974 ms apart (19.950 ms on average); emulating a frame took 1.48 ms on average, 2.10 ms at worst; the program used 11% of one CPU core. The first attempt showed 84.7%, because measuring started right at the level start, when the game stops its interrupt routine for about 1.5 s and the jiffy clock stands still.

## Part 1: the VICE program's binary monitor

S1–S6 were done; S7–S11 were dropped when D2 changed, and their jobs moved to part 2: S7 joystick → T7, S8 keyboard → T4, S9 snapshots → T8, S10 debug mode → T9, S11 screen grab → T6. The client stays in `C64Bot.ViceMonitor`, with its tests (see Open questions in `PLAN.md`).

### Protocol summary

From chapter 13 of the VICE 3.10 manual (`C:\Tools\C64\VICE-3.10\doc\vice.pdf`). All multi-byte values are little-endian.

**Command** (client → VICE), an 11-byte header followed by the body:

| Bytes | Field |
| --- | --- |
| 0 | `0x02` (STX) |
| 1 | API version, `0x02` |
| 2–5 | Body length (header not counted) |
| 6–9 | Request ID, chosen by the client and echoed in the response |
| 10 | Command type |
| 11+ | Body |

**Response** (VICE → client), a 12-byte header followed by the body:

| Bytes | Field |
| --- | --- |
| 0 | `0x02` (STX) |
| 1 | API version |
| 2–5 | Body length |
| 6 | Response type (usually the same as the command type) |
| 7 | Error code (`0x00` = OK) |
| 8–11 | Request ID; `0xFFFFFFFF` means an **event** VICE sent on its own |
| 12+ | Body |

Things that shape the client design:

- **Any command stops the emulator**, like the normal monitor. VICE then sends events on its own: register info (`0x31`) and stopped (`0x62`). The **Exit** command (`0xAA`) resumes it.
- So responses and events arrive mixed together on the same connection. The client needs one reader that matches responses to requests by request ID and hands events to whoever listens for them.
- Stopping and resuming a running VICE upsets its own speed regulation, so short visits make the game run too fast (S6, see Notes). The real-time loop therefore lets VICE stop by itself at a checkpoint, and resumes it on our own clock.

Commands part 1 used, or had planned for its dropped steps:

| Type | Command | Used in |
| --- | --- | --- |
| `0x81` | Ping | S3 |
| `0xAA` | Exit (resume) | S4 |
| `0x01` / `0x02` | Memory get / set | S5 |
| `0x12` / `0x13` | Checkpoint set / delete | S6 |
| `0xA2` | Joyport set (VICE 3.6+) | S7 |
| `0x72` | Keyboard feed (PETSCII into the keyboard buffer) | S8 |
| `0x41` / `0x42` | Dump / undump (snapshot save / load) | S9 |
| `0x84` / `0x91` | Display get / palette get | S11 |

Events: `0x11` checkpoint info (a checkpoint was hit), `0x31` register info, `0x61` JAM, `0x62` stopped, `0x63` resumed.

### Steps (part 1)

Done one step at a time, checking each before starting the next. S1–S2 need no VICE: they are plain code with unit tests. From S3 on, each step is checked against a running VICE (start command in `.claude/CLAUDE.md`).

The order put the risky parts early. S6 and S7 were to test the two things real-time play depends on: reading the game about 50 times a second without changing its speed, and setting the joystick from outside. S6 led to part 2.

- [x] **S1. Encode commands.** A type that turns a command (type, request ID, body) into bytes. Check: a unit test encodes the checkpoint example from section 13.3 of the manual and gets exactly `02 02 08 00 00 00 ad de 34 12 12 e2 fc e3 fc 01 01 04 01`.
- [x] **S2. Decode responses.** A type that reads one response (header and body) from a stream, using the body length to know where it ends. Check: unit tests decode the manual's stopped event `02 02 02 00 00 00 62 00 ff ff ff ff cf e5` (type `0x62`, no error, event, PC = `$E5CF`), and two responses sent back to back come out as two separate responses.
- [x] **S3. Connect and ping.** A client class that opens a TCP connection to `127.0.0.1:6502`, runs a background reader, and matches responses to requests by request ID. The app connects and sends a ping. Check: the app prints that the ping was answered, and VICE's window freezes, because any command stops the emulator.
- [x] **S4. Events and resume.** The client passes events (request ID `0xFFFFFFFF`) to the app, and gets an Exit command. Check: the app prints the register info and stopped events that follow the ping (with the PC), sends Exit, and VICE runs again.
- [x] **S5. Read and write memory.** Memory get and set. Check: at the `READY.` prompt, the app reads screen memory at `$0400` and prints the codes of the first characters on screen, then writes a byte there and the character appears in the top-left corner.
- [x] **S6. Real-time loop.** Read the game about 50 times a second without changing its speed. First tried: visit a running VICE 50 times a second (read a block of memory, then Exit). That made the game run too fast, so the loop now sets a checkpoint on the game's interrupt routine, lets VICE stop there by itself, reads, and sends Exit once real time has caught up with emulated time. Check: the KERNAL jiffy clock shows 100% speed over 10 seconds, and the game looks smooth. Measurements and the cause are in the Notes below.

### Notes (part 1)

- Start VICE with `-binarymonitor`; see the Commands section in `.claude/CLAUDE.md`.
- The manual lists [vice-bridge-net](https://github.com/MihaMarkic/vice-bridge-net), an existing .NET library for this protocol. We write our own (D8), but it's a good reference if something is unclear.
- **Half-updated state.** The S6 loop always stops VICE at the start of the game's interrupt routine, so a read sees the state between two runs of it. If the game also updates its state outside the interrupt routine (in its main loop), a read can still catch it halfway (for example, a guard's X position changed but not its Y position). Check this in M2.
- **VICE resumes when the connection closes** (seen in S3, 2026-11-14): after a ping the emulator froze, and it started running again as soon as the app disconnected, without an Exit command. So a crashed or stopped bot doesn't leave VICE frozen.
- **Events around a command** (seen in S4, 2026-11-14, at the `READY.` prompt):
  - A ping while running: the ping is answered, then VICE sends `RegisterInfo` (42 bytes) and `Stopped` (PC = `$E5D1`, inside the KERNAL loop that waits for a key press).
  - Exit: Exit is answered, then VICE sends `Resumed` with the same PC. VICE runs again while the app stays connected.
  - So a real-time visit ("commands, then Exit") produces three events: `RegisterInfo`, `Stopped`, `Resumed`.
- **Memory get and set work** (seen in S5, 2026-11-14): reading `$0428`–`$044F` at the `READY.` prompt returned the start-up banner (`**** COMMODORE 64 BASIC V2 ****`, after four spaces), and writing screen code 1 to `$0400` made an A appear in the top-left corner.
- **Bank IDs are still unchecked.** Memory get and set use bank 0, which doesn't matter at `$0400` (only RAM there). Before reading addresses where ROM or I/O can hide the RAM (`$A000`–`$BFFF`, `$D000`–`$FFFF`), ask VICE for its bank list with Banks available (`0x82`) and find out which ID shows what the CPU sees.
- **Measurements from S6** (2026-11-14, Lode Runner running): the app visited VICE 50 times a second for 10 seconds. Each visit read 4 KB (`$0000`–`$0FFF`) and then sent Exit, with `NoDelay` on the TCP connection.
  - 500 visits, 49.9 a second. The time between visits averaged 20.00 ms (shortest 5.74 ms, longest 32.39 ms), so the .NET timer is steady enough.
  - Visit time (memory get sent → Exit answered): 10.12 ms on average, 28.63 ms at worst. Measured that way, VICE was "frozen" 50.5% of the time.
  - VICE's status bar showed 100% and 50 fps almost the whole time, with short jumps to 102–103%. But the game visibly ran **faster** than normal.
  - Measured with the KERNAL jiffy clock (`$A0`–`$A2`, counted up 60 times per emulated second; it runs in Lode Runner): 10.03 s of emulated time in 10.00 s of real time without visits (100%), but 16.82 s in 10.02 s with visits (**168%**). So the status bar can't be trusted for this, and the visits really do speed the game up.
  - At 5, 10, 25 and 50 visits a second the speed was 103%, 110%, 121% and 167%: each visit adds roughly 10 ms of emulated time, so a lower rate only shrinks the problem.
  - Timing the parts of a visit separately (25 visits a second): the first command takes 0.3–30 ms, about 10–12 ms on average, whether it's a 4 KB read, a 1-byte read or a ping. Exit takes about 0.05 ms. So the size of the read doesn't matter: the first command waits, on average half a frame, presumably until VICE next checks for monitor commands (about once per frame). The emulator keeps running while the command waits, so the earlier "frozen" percentages were wrong.
  - **The cause, from VICE 3.10's source** (tag `3.10.0` of the [official mirror](https://github.com/VICE-Team/svn-mirror)):
    - VICE checks the binary monitor connection only at the end of each frame (`monitor_vsync_hook()`, called from `vsync_do_vsync()` in `vsync.c`). A command waits there while the emulator runs normally, which is the 0–20 ms wait.
    - Entering and leaving the monitor calls `vsync_suspend_speed_eval()` (which resets VICE's timing to "now"), `sound_suspend()` and `sound_resume()`.
    - On Windows VICE uses the DirectSound device `dx` (32 ms buffer, per `vice.log`). That device is the timing source: VICE doesn't sleep, but waits until the sound buffer has room. `dx_resume()` sets its write position back to 0 while playback carries on from where it stopped, so after every Exit the buffer looks partly or wholly empty, and VICE runs flat out to fill it. That gains up to 32 ms of emulated time per visit.
  - Every stop and every resume logs `VSync: Sync reset` in `vice.log`. Harmless, but the log grows quickly.
  - With `-sounddev dummy` (no sound, and VICE paces itself by sleeping): 115–118% at 25 visits a second instead of 119–132%. The first command waited longer: about 21 ms on average, 58 ms at worst. So sleep-based pacing also gains a few milliseconds per visit, presumably because the timing reset forgives the small lead VICE builds up between sleeps (not confirmed).
  - **Conclusion:** short visits to a running VICE make it run fast, whatever the sound device. This can't be fixed from the client.
- **Lockstep, tested in S6** (2026-11-14, Lode Runner running, default sound device):
  - The game's interrupt routine is at `$648A` (read from the vector at `$0314`–`$0315`). An exec checkpoint there is hit about 60 times per emulated second, so it's driven by the 60 Hz timer, not once per frame. Once, the stops paused for 1.2 s (probably the game turning interrupts off for a moment).
  - Stop there, read 4 KB, Exit straight away: VICE never gets to sleep and runs at **1,254%**, effectively warp. Our response took 0.15 ms on average, 1.7 ms at worst.
  - Stop there, read 4 KB, and hold Exit until as much real time has passed as emulated time (from the jiffy clock): **100% speed** (10.00 s emulated in 9.98 s real), 60.1 stops a second. VICE needed about 2 ms per 16.7 ms step; the read took 0.47 ms on average, and each stop was held back for 14.2 ms on average. The waiting was done by spinning on a stopwatch, because Windows' normal timers tick every 15.6 ms.
  - The game looked smooth, even though the 60 Hz steps don't line up with the 50 Hz frames.
  - So the bot can pace the game itself, and every read sees the game at the same point. D4 was changed to this.
- **Can VICE keep its own pace while a program talks to it?** Checked in VICE 3.10's source, 2026-11-14:
  - No setting for it. The binary monitor has only two settings (`BinaryMonitorServer`, `BinaryMonitorServerAddress`). Any incoming command makes VICE enter the monitor at the end of the frame (`monitor_check_binary()`), and entering and leaving always resets the speed regulation and suspends the sound (`monitor_open()`, `monitor_close()`). VICE's comment: so that "we don't display bogus speed values". The monitor is built as a debugger for pauses of human length.
  - Every Windows sound device shifts the timing on a stop and resume in its own way: `dx` resets its write position, `wmm` puts its write position right behind the playback position, `sdl` forgets that its buffer was full. With `dummy`, VICE sleeps using a standard Windows waitable timer, so it emulates in bursts.
  - VICE's network play exchanges joystick input every frame without entering the monitor, but with at least 5 frames of delay (`network.c`), and it can't read memory.
  - Alternatives outside the VICE app: [vice-libretro](https://github.com/libretro/vice-libretro) is a port of VICE 3.10 (including x64sc) as a libretro core, a DLL that a host program runs one frame per call. It hands the host a pointer to the 64 KB RAM (`retro_get_memory_data`) and saves and loads snapshots in memory. Prebuilt Windows DLLs are on the libretro build server. RetroArch, the usual host, can also read and write core memory over UDP between frames (`READ_CORE_MEMORY`, up to 16 KB per request) and take gamepad input over the network, without pausing the core.
- **Follow-ups from S6** (no longer needed after part 2, except that the high-resolution timer became `FrameClock` in T5):
  - The wait spins on the stopwatch and keeps a CPU core busy. Use a high-resolution timer instead, and spin only for the last moment.
  - Sound wasn't checked. Every stop suspends and resumes VICE's sound output, 60 times a second, so it may sound choppy.
  - If the game ever looks jerky, look for a point that runs once per frame instead of the 60 Hz interrupt routine.
  - The game's interrupt routine stopped running once for 1.2 s. The loop must cope with gaps like that, and with the game switching to another interrupt routine (for example between levels).
