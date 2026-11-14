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

## Steps

To be broken into small steps, starting with the smallest thing that proves the connection works: open a TCP connection to VICE, send a ping and get an answer back.

## Notes

- Start VICE with `-binarymonitor`; see the Commands section in `.claude/CLAUDE.md`.
