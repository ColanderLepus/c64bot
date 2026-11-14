# M1: Connect C# to VICE

**Status:** Not started

Build the binary monitor client in `C64Bot.Vice`, so the program can talk to a running VICE: read and write memory, control the emulator and send input.

## Tasks

- [ ] Connect and ping
- [ ] Read and write memory
- [ ] Pause, step and resume; break once per frame (breakpoint on the main loop or interrupt handler)
- [ ] Set joystick state and send keys
- [ ] Grab the screen
- [ ] Save and load snapshots

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

Commands we'll use in M1:

| Type | Command | Used in |
| --- | --- | --- |
| `0x81` | Ping | S3 |
| `0xAA` | Exit (resume) | S4 |
| `0x01` / `0x02` | Memory get / set | S5 |
| `0xA2` | Joyport set (VICE 3.6+) | S6 |
| `0x72` | Keyboard feed (PETSCII into the keyboard buffer) | S7 |
| `0x12` | Checkpoint set | S8 |
| `0x84` / `0x91` | Display get / palette get | S9 |
| `0x41` / `0x42` | Dump / undump (snapshot save / load) | S10 |

Events: `0x31` register info, `0x61` JAM, `0x62` stopped, `0x63` resumed.

## Steps

Done one step at a time, checking each before starting the next. S1–S2 need no VICE: they are plain code with unit tests. From S3 on, each step is checked against a running VICE (start command in `.claude/CLAUDE.md`).

- [ ] **S1. Encode commands.** A type that turns a command (type, request ID, body) into bytes. Check: a unit test encodes the checkpoint example from section 13.3 of the manual and gets exactly `02 02 08 00 00 00 ad de 34 12 12 e2 fc e3 fc 01 01 04 01`.
- [ ] **S2. Decode responses.** A type that reads one response (header and body) from a stream, using the body length to know where it ends. Check: unit tests decode the manual's stopped event `02 02 02 00 00 00 62 00 ff ff ff ff cf e5` (type `0x62`, no error, event, PC = `$E5CF`), and two responses sent back to back come out as two separate responses.
- [ ] **S3. Connect and ping.** A client class that opens a TCP connection to `127.0.0.1:6502`, runs a background reader, and matches responses to requests by request ID. The app connects and sends a ping. Check: the app prints that the ping was answered, and VICE's window freezes, because any command stops the emulator.
- [ ] **S4. Events and resume.** The client passes events (request ID `0xFFFFFFFF`) to the app, and gets an Exit command. Check: the app prints the register info and stopped events that follow the ping (with the PC), sends Exit, and VICE runs again.
- [ ] **S5. Read and write memory.** Memory get and set. Check: at the `READY.` prompt, the app reads screen memory at `$0400` and prints the codes of the first characters on screen, then writes a byte there and the character appears in the top-left corner.
- [ ] **S6. Joystick.** Joyport set. First find out from the manual or by testing how ports are numbered and which bits mean up, down, left, right and fire. Check: in Lode Runner the app holds the joystick in port 2 to the right for a moment and the player walks right.
- [ ] **S7. Keyboard.** Keyboard feed. Check: on version A's trainer menu, the app types `N`, `N` and a space, and the game starts with the trainer off. Note: this writes to the keyboard buffer, so it works for menus that read keys through the KERNAL but probably not for keys the game scans itself (like C= for level skip). Record what works in [lode-runner.md](../lode-runner.md).
- [ ] **S8. Once per frame.** Find an address that runs exactly once per frame (start with the interrupt handler: read the vectors at `$0314` and `$FFFE`), and set a checkpoint there that stops the emulator. The app loops: wait for stopped, read memory, Exit. Check: the app counts about 50 stops per second (PAL), and prints how fast the loop runs when it does real work.
- [ ] **S9. Grab the screen.** Display get and palette get, saved as an image file. Choose the image format and library in this step. Check: the saved image matches what VICE shows.
- [ ] **S10. Snapshots.** Dump and undump. Check: save a snapshot in Lode Runner, move the player, load the snapshot, and the player is back where it was.

## Notes

- Start VICE with `-binarymonitor`; see the Commands section in `.claude/CLAUDE.md`.
- The manual lists [vice-bridge-net](https://github.com/MihaMarkic/vice-bridge-net), an existing .NET library for this protocol. We write our own (D8), but it's a good reference if something is unclear.
