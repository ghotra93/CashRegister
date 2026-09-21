---
description: Run /help — see .claude/commands/help.md for the authoritative spec.
argument-hint: see .claude/commands/help.md

---
# /help

**Phase:** meta — read-only
**Owning agent:** none

## Purpose
Print the command catalog and the recommended phase order. Optionally explain a single command in depth.

## Inputs
- Optional `<command-name>` (without the leading slash).

## Reads
- `.claude/commands/`
- `.claude/commands/<command-name>.md` if a name was supplied.
- `.specs/_stack.json` if present, to say which stack's commands apply here.

## Writes
Nothing.

## Process
- No argument → print the table from `.claude/commands/` and the natural-language alias list, grouped by stack:
  - **Shared:** `/status`, `/help`.
  - **JVM (Spring Boot 4, Maven):** `/spec`, `/spec-review`, `/epic-plan`, `/plan`, `/build`, `/test`, `/validate`, `/review`, `/ship`, `/onboard`, `/wire-harness`, `/code-simplify`.
  - **.NET (ASP.NET Core 10):** the same twelve prefixed with `net-` — `/net-spec`, `/net-spec-review`, `/net-epic-plan`, `/net-plan`, `/net-build`, `/net-test`, `/net-validate`, `/net-review`, `/net-ship`, `/net-onboard`, `/net-wire-harness`, `/net-code-simplify`.
  Note which stack the repo is on, from `.specs/_stack.json` `language` when it exists, and say that the natural-language aliases resolve to the JVM commands — a .NET user must name the `/net-*` command explicitly.
- With argument → print the contents of the matching shared command file (Purpose, Inputs, Reads, Writes, Process, Refuse if, Done when).

## Refuse if
Never.

## Done when
Help text is rendered.
