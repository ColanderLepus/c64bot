# M0: Project setup

**Status:** Done (2026-11-14)

Get everything in place to start coding: the emulator, the game, the main technical choices and an empty .NET solution.

## Tasks

- [x] Create repo and plan document
- [x] Check which VICE version is installed (binary monitor needs 3.5 or later): VICE 3.10
- [x] Get a Lode Runner image (disk or cartridge): `Lode Runner.d64`, see [Environment](../PLAN.md#environment)
- [x] Choose which version on the disk to use: version A (D6)
- [x] Choose a .NET version: .NET 10 (D7)
- [x] Decide: our own binary monitor client or an existing library: our own (D8)
- [x] Create the solution skeleton (steps below)

## Solution skeleton

Done one step at a time, checking each step before starting the next. Project names use the prefix `C64Bot` (confirmed in S1). The Lode Runner game library (`C64Bot.LodeRunner`) is added later, in M2, when there is something to put in it.

- [x] **S1. Solution and shared settings.** Create `src/` and `tests/` folders, the solution file `C64Bot.slnx`, a .NET `.gitignore`, and a `Directory.Build.props` that makes every project target `net10.0` with nullable reference types and implicit usings on. Check: `dotnet sln list` runs and shows no projects yet.
- [x] **S2. Emulator library.** Class library `src/C64Bot.Vice`, added to the solution. This is where the binary monitor client will live. Check: `dotnet build` succeeds.
- [x] **S3. Test project.** xUnit project `tests/C64Bot.Vice.Tests`, referencing the emulator library, with one placeholder test. Check: `dotnet test` passes.
- [x] **R. Restructure.** Moved the solution, `Directory.Build.props`, `src/` and `tests/` into `C64Bot/`, `PLAN.md` into `docs/` and `CLAUDE.md` into `.claude/` (D9). The paths in S1–S3 are from before this move. Check: `git status` shows only renames, and `dotnet test C64Bot` passes.
- [x] **S4. Console app.** `C64Bot/src/C64Bot.App`, referencing the emulator library, printing one line. Check: `dotnet run --project C64Bot/src/C64Bot.App` prints it.
- [x] **S5. Commands in CLAUDE.md.** Add the build, test and run commands to `.claude/CLAUDE.md` so future sessions know them.

## Notes

- On 2026-11-15 `C64Bot.Vice` and `C64Bot.Vice.Tests` were renamed `C64Bot.ViceMonitor` and `C64Bot.ViceMonitor.Tests`, when the emulator layer moved to `C64Bot.Libretro` (D2). S2 and S3 above use the old names.
- Each template-generated project file repeats `TargetFramework`, `ImplicitUsings` and `Nullable`. Delete them so `Directory.Build.props` is the only place they're set.
