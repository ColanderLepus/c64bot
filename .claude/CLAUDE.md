# CLAUDE.md

A C# program that plays Commodore 64 games running in the VICE emulator, starting with Lode Runner.

## Plan document

[docs/PLAN.md](../docs/PLAN.md) tracks the work across sessions. It holds the decisions, architecture, repository layout, milestones, Lode Runner RAM map, open questions and session log.

- **At the start of a session:** read `docs/PLAN.md` before doing anything else, and pick up from the current milestone.
- **When work moves forward:** tick off checkboxes and update the Status section, including the date.
- **When a decision is made or changed:** add or edit a row in the Decisions table and give the reason.
- **When a RAM address is found:** add it to the RAM map, with notes on how it was confirmed.
- **When an open question is answered:** remove it from Open questions and record the answer where it belongs (Decisions, RAM map or a milestone).
- **At the end of a session:** add a short entry to the Session log saying what was done and what comes next.

Don't change the plan's direction (milestone order, architecture, decisions) without checking with the owner first.

## Conventions

- The .NET solution lives in `C64Bot/`, not in the repo root. Documentation lives in `docs/`. Keep the repo root for repo-wide files only.
- Markdown files follow markdownlint style: a blank line after headings and around lists.
