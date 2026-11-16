# CLAUDE.md

A C# program that plays Commodore 64 games, starting with Lode Runner. The emulator runs inside the program as a libretro core; the core we use is VICE 3.10's x64sc (D2 in `docs/PLAN.md`).

## Plan documents

The work is tracked across sessions in `docs/`:

- [docs/PLAN.md](../docs/PLAN.md): the overview. Status, environment, decisions, architecture, repository layout, milestone list, open questions and session log.
- [docs/milestones/](../docs/milestones/): one document per started milestone, with its tasks, small steps and notes.
- [docs/lode-runner.md](../docs/lode-runner.md): game reference. Versions on the disk, game notes and the RAM map.

How to keep them current:

- **At the start of a session:** read `docs/PLAN.md`, then the current milestone's document, before doing anything else.
- **When work moves forward:** tick off checkboxes in the milestone document. Update the Status section and the milestone table in `PLAN.md`, including the date.
- **When a milestone starts:** create its document in `docs/milestones/` (name it like `M2-find-ram-data.md`), move its scope list there from `PLAN.md`, and link it in the milestone table.
- **When a decision is made or changed:** add or edit a row in the Decisions table in `PLAN.md` and give the reason. Decisions live only there.
- **When something is learned about the game:** add it to `docs/lode-runner.md`. RAM addresses go in the RAM map, with notes on how they were confirmed.
- **When an open question is answered:** remove it from Open questions and record the answer where it belongs.
- **At the end of a session:** add a short entry to the Session log in `PLAN.md` saying what was done and what comes next.

Don't change the plan's direction (milestone order, architecture, decisions) without checking with the owner first.

## How we work

- One step at a time, checking each step before starting the next.
- Claude implements the changes, but first shows the owner what it plans to change (the code, or a clear description of it) and waits for the go-ahead.
- After a step: check it (build, tests, a run), update the plan documents, and write a commit message when the owner asks. Commit only when told to.
- Claude runs the checks itself: headless, driving the core from a script, with the core's pictures saved as PNG files and looked at where the screen matters. The owner is asked only for what needs a person watching in real time (the window's smoothness, its keyboard, how playing feels), and a plan says when that's the case.

## Commands

Run from the repo root:

```powershell
dotnet build C64Bot                          # build all projects
dotnet test C64Bot                           # run all tests
dotnet run --project C64Bot/src/C64Bot.App          # run the app: starts a new game at level 1 and opens its window
dotnet run --project C64Bot/src/C64Bot.App -- 5     # the same, at level 5 (1-150)

# The VICE program, for exploring by hand (its monitor and debugger):
C:\Tools\C64\VICE-3.10\bin\x64sc.exe                                                         # at the BASIC READY. prompt
C:\Tools\C64\VICE-3.10\bin\x64sc.exe -autostart "C:\Emulation\ROMs\C64\Lode Runner.d64"      # with Lode Runner version A
```

The app needs the core DLL at `C:\Tools\C64\libretro\vice_x64sc_libretro.dll` (see Environment in `docs/PLAN.md`) and the Lode Runner disk, but not the VICE program. So do the tests in `C64Bot.Libretro.Tests` and `C64Bot.LodeRunner.Tests` that run the core; they're skipped (not failed) on a PC without them. In the app's console, T and L save and load a state, R and D compare RAM, and G prints the game state. VICE is a GUI program, so PowerShell doesn't wait for it: the terminal is free again as soon as VICE starts.

## Conventions

- The owner's terminal is PowerShell 7 (`pwsh.exe`) in VS Code. Write any command meant for the owner to run in PowerShell syntax, not bash or cmd.
- The .NET solution lives in `C64Bot/`, not in the repo root. Documentation lives in `docs/`. Keep the repo root for repo-wide files only.
- Markdown files follow markdownlint style: a blank line after headings and around lists.
- Names in code follow the terminology of what they wrap: `libretro.h`'s in `C64Bot.Libretro` (for example core, frontend, environment command, system info, core option). This always wins over other naming arguments, so don't debate it: just use their term.
- XML documentation (`<summary>` and friends):
  - Every public type gets a `<summary>`: one sentence saying what it is.
  - Public members get one only when the name isn't enough (what it really does, an edge case, why it exists). No summaries that just repeat the name, like "Gets the frame width."
  - Enum values get one only when the value needs explaining.
  - No XML docs on private code or tests. Private code gets a `//` comment if something needs explaining; tests are explained by their names and Arrange/Act/Assert sections.
  - Write for a reader who knows C# but not VICE or the C64. Explain terms like libretro core, frontend, environment command, save state, or the game's main loop and its passes in plain words where a type depends on them. `libretro.h` and `docs/lode-runner.md` are for details, not for basic understanding: point to them in `<remarks>`.
  - Style: a type's summary is a noun phrase ("A copy of the emulated machine's RAM ..."), a method's starts with a verb ("Reads the state from the C64's RAM ..."). Details that don't fit in one sentence go in `<remarks>`. Use `<param>` on constructors when a parameter needs explaining, and `<exception>` for exceptions a caller should handle.
  - Not enforced by the compiler (no CS1591 warnings); check it when writing or reviewing code.
- Tests are split into clear `// Arrange`, `// Act` and `// Assert` sections wherever possible. Act is a separate step, not folded into the assertion.
