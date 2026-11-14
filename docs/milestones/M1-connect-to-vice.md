# M1: Connect C# to VICE

**Status:** In progress (S1–S6 done)

Build the binary monitor client in `C64Bot.Vice`, so the program can talk to a running VICE: read and write memory, send input and control the emulator. The bot plays at real-time speed (D4), so M1 also has to find a way to read and control the game about 50 times a second without changing its speed.

## Tasks

- [x] Connect and ping
- [x] Read and write memory
- [x] Real-time loop, paced by the bot, that keeps the game at 100% speed
- [ ] Joystick input from inside the loop
- [ ] Keyboard input
- [ ] Save and load snapshots
- [ ] Debug mode: one step per key press
- [ ] Grab the screen

## Protocol summary

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

Commands we'll use in M1:

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

## Steps

Done one step at a time, checking each before starting the next. S1–S2 need no VICE: they are plain code with unit tests. From S3 on, each step is checked against a running VICE (start command in `.claude/CLAUDE.md`).

The order puts the risky parts early. S6 and S7 test the two things real-time play depends on: reading the game about 50 times a second without changing its speed, and setting the joystick from outside. If either fails, we revisit D4 or D2 before building the rest. (S6 did change D4; see Notes.)

### Foundation

- [x] **S1. Encode commands.** A type that turns a command (type, request ID, body) into bytes. Check: a unit test encodes the checkpoint example from section 13.3 of the manual and gets exactly `02 02 08 00 00 00 ad de 34 12 12 e2 fc e3 fc 01 01 04 01`.
- [x] **S2. Decode responses.** A type that reads one response (header and body) from a stream, using the body length to know where it ends. Check: unit tests decode the manual's stopped event `02 02 02 00 00 00 62 00 ff ff ff ff cf e5` (type `0x62`, no error, event, PC = `$E5CF`), and two responses sent back to back come out as two separate responses.
- [x] **S3. Connect and ping.** A client class that opens a TCP connection to `127.0.0.1:6502`, runs a background reader, and matches responses to requests by request ID. The app connects and sends a ping. Check: the app prints that the ping was answered, and VICE's window freezes, because any command stops the emulator.
- [x] **S4. Events and resume.** The client passes events (request ID `0xFFFFFFFF`) to the app, and gets an Exit command. Check: the app prints the register info and stopped events that follow the ping (with the PC), sends Exit, and VICE runs again.
- [x] **S5. Read and write memory.** Memory get and set. Check: at the `READY.` prompt, the app reads screen memory at `$0400` and prints the codes of the first characters on screen, then writes a byte there and the character appears in the top-left corner.

### Real time: the risky parts

- [x] **S6. Real-time loop.** Read the game about 50 times a second without changing its speed. First tried: visit a running VICE 50 times a second (read a block of memory, then Exit). That made the game run too fast, so the loop now sets a checkpoint on the game's interrupt routine, lets VICE stop there by itself, reads, and sends Exit once real time has caught up with emulated time. Check: the KERNAL jiffy clock shows 100% speed over 10 seconds, and the game looks smooth. Measurements and the cause are in the Notes below.
- [ ] **S7. Joystick.** Joyport set, used from inside the real-time loop. First find out how ports are numbered and which bits mean up, down, left, right and fire (manual, vice-bridge-net, or testing). Then find out whether a value stays set after Exit until it's changed, or whether VICE's own joystick handling overwrites it. Check: in Lode Runner the app holds right for one second, then releases, and the player walks right and stops. If the value doesn't hold, stop and revisit D2.

### The rest of the toolkit

- [ ] **S8. Keyboard.** Keyboard feed. Check: on version A's trainer menu, the app types `N`, `N` and a space, and the game starts with the trainer off. Note: this writes to the keyboard buffer, so it works for menus that read keys through the KERNAL but probably not for keys the game scans itself (like C= for level skip). Record what works in [lode-runner.md](../lode-runner.md).
- [ ] **S9. Snapshots.** Dump and undump. Check: save a snapshot in Lode Runner, move the player, load the snapshot, and the player is back where it was. Snapshots also give every test run the same starting point, without going through loading and the trainer menu.
- [ ] **S10. Debug mode.** The S6 loop with a different rule for sending Exit: on each key press instead of on time. Check: each key press advances the game by one step (one run of its interrupt routine), and holding the key down runs it in slow motion.
- [ ] **S11. Grab the screen.** Display get and palette get, saved as an image file. Choose the image format and library in this step. Check: the saved image matches what VICE shows. This feeds the debug view later, and the screen-reading fallback (D3).

## Notes

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
- **Follow-ups from S6:**
  - The wait spins on the stopwatch and keeps a CPU core busy. Use a high-resolution timer instead, and spin only for the last moment.
  - Sound wasn't checked. Every stop suspends and resumes VICE's sound output, 60 times a second, so it may sound choppy.
  - If the game ever looks jerky, look for a point that runs once per frame instead of the 60 Hz interrupt routine.
  - The game's interrupt routine stopped running once for 1.2 s. The loop must cope with gaps like that, and with the game switching to another interrupt routine (for example between levels).
