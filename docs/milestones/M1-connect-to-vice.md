# M1: Connect C# to VICE

**Status:** In progress (S1–S3 done)

Build the binary monitor client in `C64Bot.Vice`, so the program can talk to a running VICE: read and write memory, send input and control the emulator. The bot plays in real time (D4), so M1 also has to show that talking to VICE doesn't noticeably slow the game down.

## Tasks

- [ ] Connect and ping
- [ ] Read and write memory
- [ ] Real-time loop at about 50 Hz, with the cost of each freeze measured
- [ ] Joystick input that works while the game runs
- [ ] Keyboard input
- [ ] Save and load snapshots
- [ ] Lockstep debug mode: stop once per frame
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
- In real-time mode every visit is "commands, then Exit". Emulated time stands still in between, so the game is unaffected; only wall-clock time is lost. S6 measures how much.

Commands we'll use in M1:

| Type | Command | Used in |
| --- | --- | --- |
| `0x81` | Ping | S3 |
| `0xAA` | Exit (resume) | S4 |
| `0x01` / `0x02` | Memory get / set | S5 |
| `0xA2` | Joyport set (VICE 3.6+) | S7 |
| `0x72` | Keyboard feed (PETSCII into the keyboard buffer) | S8 |
| `0x41` / `0x42` | Dump / undump (snapshot save / load) | S9 |
| `0x12` | Checkpoint set | S10 |
| `0x84` / `0x91` | Display get / palette get | S11 |

Events: `0x31` register info, `0x61` JAM, `0x62` stopped, `0x63` resumed.

## Steps

Done one step at a time, checking each before starting the next. S1–S2 need no VICE: they are plain code with unit tests. From S3 on, each step is checked against a running VICE (start command in `.claude/CLAUDE.md`).

The order puts the risky parts early. S6 and S7 test the two assumptions real-time play depends on: that a freeze is short, and that the joystick can be set from outside while the game runs. If either fails, we revisit D4 or D2 before building the rest.

### Foundation

- [x] **S1. Encode commands.** A type that turns a command (type, request ID, body) into bytes. Check: a unit test encodes the checkpoint example from section 13.3 of the manual and gets exactly `02 02 08 00 00 00 ad de 34 12 12 e2 fc e3 fc 01 01 04 01`.
- [x] **S2. Decode responses.** A type that reads one response (header and body) from a stream, using the body length to know where it ends. Check: unit tests decode the manual's stopped event `02 02 02 00 00 00 62 00 ff ff ff ff cf e5` (type `0x62`, no error, event, PC = `$E5CF`), and two responses sent back to back come out as two separate responses.
- [x] **S3. Connect and ping.** A client class that opens a TCP connection to `127.0.0.1:6502`, runs a background reader, and matches responses to requests by request ID. The app connects and sends a ping. Check: the app prints that the ping was answered, and VICE's window freezes, because any command stops the emulator.
- [ ] **S4. Events and resume.** The client passes events (request ID `0xFFFFFFFF`) to the app, and gets an Exit command. Check: the app prints the register info and stopped events that follow the ping (with the PC), sends Exit, and VICE runs again.
- [ ] **S5. Read and write memory.** Memory get and set. Check: at the `READY.` prompt, the app reads screen memory at `$0400` and prints the codes of the first characters on screen, then writes a byte there and the character appears in the top-left corner.

### Real time: the risky parts

- [ ] **S6. Real-time loop and the cost of a freeze.** A loop that runs about 50 times a second: read a block of memory, then Exit. Measure how long each visit takes (from the first command to the Exit answer) and how steady the 50 Hz timing is. Check: while Lode Runner runs, VICE's status bar stays at about 100% speed and 50 fps, and the app prints the average and worst visit time. Record the numbers in the Notes below. If VICE drops noticeably below full speed, stop and revisit D4.
- [ ] **S7. Joystick.** Joyport set, used from inside the real-time loop. First find out how ports are numbered and which bits mean up, down, left, right and fire (manual, vice-bridge-net, or testing). Then find out whether a value stays set after Exit until it's changed, or whether VICE's own joystick handling overwrites it. Check: in Lode Runner the app holds right for one second, then releases, and the player walks right and stops. If the value doesn't hold, stop and revisit D2.

### The rest of the toolkit

- [ ] **S8. Keyboard.** Keyboard feed. Check: on version A's trainer menu, the app types `N`, `N` and a space, and the game starts with the trainer off. Note: this writes to the keyboard buffer, so it works for menus that read keys through the KERNAL but probably not for keys the game scans itself (like C= for level skip). Record what works in [lode-runner.md](../lode-runner.md).
- [ ] **S9. Snapshots.** Dump and undump. Check: save a snapshot in Lode Runner, move the player, load the snapshot, and the player is back where it was. Snapshots also give every test run the same starting point, without going through loading and the trainer menu.
- [ ] **S10. Lockstep debug mode.** Find an address that runs exactly once per frame (start with the interrupt handler: read the vectors at `$0314` and `$FFFE`), and set a checkpoint there that stops the emulator. The app then steps one frame each time you press a key. Check: each key press advances the game by exactly one frame, and holding the key down runs it in slow motion.
- [ ] **S11. Grab the screen.** Display get and palette get, saved as an image file. Choose the image format and library in this step. Check: the saved image matches what VICE shows. This feeds the debug view later, and the screen-reading fallback (D3).

## Notes

- Start VICE with `-binarymonitor`; see the Commands section in `.claude/CLAUDE.md`.
- The manual lists [vice-bridge-net](https://github.com/MihaMarkic/vice-bridge-net), an existing .NET library for this protocol. We write our own (D8), but it's a good reference if something is unclear.
- **Half-updated state.** In real-time mode the bot freezes VICE at a random point in the game's code, so the game may be halfway through updating something (for example, a guard's X position changed but not its Y position). Look for this in M2. Possible fixes: read twice and compare, or wait for a frame counter to change.
- **VICE resumes when the connection closes** (seen in S3, 2026-11-14): after a ping the emulator froze, and it started running again as soon as the app disconnected, without an Exit command. So a crashed or stopped bot doesn't leave VICE frozen.
- **Measurements from S6:** not yet measured.
