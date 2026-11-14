# CLAUDE.md

A C# program that plays Commodore 64 games running in the VICE emulator, starting with Lode Runner.

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

## Commands

Run from the repo root:

```powershell
dotnet build C64Bot                          # build all projects
dotnet test C64Bot                           # run all tests
dotnet run --project C64Bot/src/C64Bot.App   # run the app

# Start VICE with the binary monitor on (TCP port 6502), at the BASIC READY. prompt
C:\Tools\C64\VICE-3.10\bin\x64sc.exe -binarymonitor

# Start VICE with the binary monitor on and autostart Lode Runner version A
C:\Tools\C64\VICE-3.10\bin\x64sc.exe -binarymonitor -autostart "C:\Emulation\ROMs\C64\Lode Runner.d64"
```

VICE is a GUI program, so PowerShell doesn't wait for it: the terminal is free again as soon as VICE starts.

## Conventions

- The owner's terminal is PowerShell 7 (`pwsh.exe`) in VS Code. Write any command meant for the owner to run in PowerShell syntax, not bash or cmd.
- The .NET solution lives in `C64Bot/`, not in the repo root. Documentation lives in `docs/`. Keep the repo root for repo-wide files only.
- Markdown files follow markdownlint style: a blank line after headings and around lists.
- Names in code follow the VICE manual's terminology (for example command, response, body, request ID, memspace, checkpoint). This always wins over other naming arguments, so don't debate it: just use VICE's term.
- XML documentation (`<summary>` and friends):
  - Every public type gets a `<summary>`: one sentence saying what it is.
  - Public members get one only when the name isn't enough (what it really does, an edge case, why it exists). No summaries that just repeat the name, like "Gets the request ID."
  - Enum values get one only when the value needs explaining.
  - No XML docs on private code or tests. Private code gets a `//` comment if something needs explaining; tests are explained by their names and Arrange/Act/Assert sections.
  - Write for a reader who knows C# but not VICE or the C64. Explain terms like binary monitor, event, request ID, snapshot or checkpoint in plain words where a type depends on them. The manual is for details, not for basic understanding: point to the section in `<remarks>`.
  - Style: a type's summary is a noun phrase ("A command for VICE's binary monitor ..."), a method's starts with a verb ("Reads exactly one response ..."). Details that don't fit in one sentence go in `<remarks>`. Use `<param>` on constructors when a parameter needs explaining, and `<exception>` for exceptions a caller should handle.
  - Not enforced by the compiler (no CS1591 warnings); check it when writing or reviewing code.
- Tests are split into clear `// Arrange`, `// Act` and `// Assert` sections wherever possible. Act is a separate step, not folded into the assertion.
