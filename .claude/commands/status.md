---
description: Run /status — see .claude/commands/status.md for the authoritative spec.
argument-hint: see .claude/commands/status.md

---
# /status

**Phase:** meta — read-only
**Owning agent:** none (pure reporting)

## Purpose
Show the user where every active feature stands. No writes, no side effects.

## Inputs
- Optional `<feature-id>`; without it, summarize all features under `.specs/`.

## Reads
- `.specs/*/01-spec.md`, `02-spec-review.md`, `03-epic-design.md`, `03a-epic-roadmap.md`, `03-design.md`, `04-tasks.md`, `.tdd-state.json`, `07-validation-report.md`.
- `.specs/_stack.json` if present — `language` tells you which stack the repo is on (`dotnet`, else JVM).
- `target/harness-summary.json` (JVM harness) or `artifacts/harness-summary.json` (.NET harness), whichever is present. Both files use the same schema, so read either one the same way.

## Writes
Nothing.

## Process
For each feature (or the supplied one), produce a one-row-per-feature table:
- `feature_id`
- `stack` — from `.specs/_stack.json` `language`; `—` when the file is absent.
- `phase` — derived from which artifacts exist + verdicts (specify, spec-review, plan-epic, plan, build, test, validate, review, done).
- `acs_total`, `acs_with_tests`
- `tasks_done / tasks_total`
- `last_validate_verdict` and timestamp
- `active_task` from `.tdd-state.json` (if any) and its current `phase`

Then print a single sentence: "Recommended next action: …". Name the command for the feature's stack — `/build` on the JVM, `/net-build` on .NET.

## Refuse if
Never. This command never refuses; if data is missing it shows `—`.

## Done when
Status table is printed and a recommended next command is suggested.
