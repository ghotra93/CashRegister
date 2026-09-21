---
description: Run /net-build — see .claude/commands/net-build.md for the authoritative spec.
argument-hint: see .claude/commands/net-build.md
agent: dotnet-implementer
---
# /net-build

**Phase:** 4 — build (TDD)
**Owning agent:** `.claude/agents/dotnet-implementer.md` (collaborates with `dotnet-test-engineer`)
**Skills used:** `tdd-red-green-refactor`, `dotnet-10-conventions`, `clarity-over-cleverness`, `dotnet-testing-patterns`, `dotnet-task-decomposition`

## Purpose
Execute one task end-to-end through the four TDD phases (red → green → refactor → simplify), updating `.tdd-state.json` and appending a block to `05-implementation-log.md` after each phase.

## Inputs
- `<task-id>` (e.g. `T-001`). Required.

## Reads
- `.specs/<feature-id>/04-tasks.md`
- `.specs/<feature-id>/03-design.md`
- `.specs/<feature-id>/.tdd-state.json`
- `.claude/skills/tdd-red-green-refactor/SKILL.md` (authoritative)
- `.claude/skills/dotnet-testing-patterns/SKILL.md` (scope selection and the `[Trait("AC", ...)]` contract)

## Writes
- `tests/<Module>.Tests/**`, `tests/<Module>.IntegrationTests/**` and `src/<Module>/Features/<Slice>/**` files listed in `tasks[<task-id>].files_in_scope`.
- `.specs/<feature-id>/.tdd-state.json` (phase transitions; `tasks` is a map keyed by task id with a top-level `active_task`).
- `.specs/<feature-id>/05-implementation-log.md` (one `### <task-id> — <phase>` block per phase).

## Process
For the supplied `<task-id>`:

0. **Pre-flight commit check.** Run `git status`. If there are uncommitted changes from any prior task, refuse to start. List the changed files and remind: `git commit → /net-build <task-id>`.
1. **Activate.** Set `.tdd-state.json` `active_task = <task-id>`. Refuse if any other task is `phase: red|green|refactor|simplify` (one task in flight at a time).
2. **Red** (`dotnet-test-engineer`). Write the smallest test that captures the next AC slice, with the attribute block in order: `[Fact]`/`[Theory]` → `[Trait("AC", "AC-NNN")]` → `[Trait("Category", "Integration")]` when it needs a container or the full host. Run it with `dotnet test --filter "FullyQualifiedName~ClassName.MethodName"`. Confirm it fails on an assertion, not a compile error. Capture the failure message into `tasks[<task-id>].red_failure_excerpt`. Set `phase: "red"`. Append log block.
3. **Green** (`dotnet-implementer`). Write the minimum production code under `files_in_scope` to pass the test. Re-run the single test, then `dotnet test --filter "Category!=Integration"` for the unit suite (plus `dotnet test --filter "Category=Integration"` when integration files were touched). Set `phase: "green"`. Append log block.
4. **Refactor.** Improve structure without changing behavior or public signatures. Re-run the suite after every edit. Set `phase: "refactor"`. Append log block.
5. **Simplify.** Apply `clarity-over-cleverness` (untangle ternaries, inline once-used helpers, kill dead options, name domain concepts from the `01-spec.md` glossary, extract every literal that appears 2+ times in a file). Run `dotnet format --verify-no-changes` and `dotnet build -c Release` (warning-free) plus the suite. Set `phase: "simplify"`. Append log block.
6. **Done.** Set `phase: "done"`, clear `active_task`. If more ACs in this task remain uncovered, immediately re-run from step 2 with the next slice (do not declare done early).
7. **STOP — commit reminder.** Surface: files changed (`git status`), tests passing, suggested commit message. Recommend: `git status → git commit → /net-build <next-task-id>`. Do not auto-start the next task unless the user explicitly requests chaining.

## Refuse if
- `<task-id>` is not in `.tdd-state.json`, or `.tdd-state.json` has an array-shaped `tasks` value (the hooks reject it — re-run `/net-plan`).
- Another task is mid-flight (not `pending` or `done`).
- A `src/**/*.cs` edit is attempted while `phase != "red"` and `red_failure_excerpt` is empty (also enforced by the Claude hook `block-impl-without-failing-test.sh`).
- Any edit lands outside `tasks[<task-id>].files_in_scope` (enforced by `enforce-files-in-scope.sh`).
- A skip or gate-weakening flag would be needed — `dotnet test --no-build`, `-p:SkipTests`, `-p:RunAnalyzers=false`, `-p:TreatWarningsAsErrors=false` (enforced by `forbid-skip-flags.sh`).
- The spec or design leaves the edge case undefined — halt and open a `Q-NNN` instead of choosing a silent default (`block-progress-on-open-questions.sh`).
- A new NuGet package would be needed and the user has not confirmed it (version goes in `Directory.Packages.props`, never inline on the `PackageReference`).

## Done when
- Every AC listed under `tasks[<task-id>].acs_covered` has at least one test carrying `[Trait("AC", "AC-NNN")]`.
- All four phase blocks are present in `05-implementation-log.md` for this task.
- `.tdd-state.json` shows `phase: "done"` for `<task-id>` and `active_task` is cleared.
- `dotnet format --verify-no-changes` is clean and `dotnet build -c Release` produces no warnings.
- Commit reminder surfaced (Step 7); user commits before starting the next task. After all tasks done, run `/net-test` then `/net-validate`.
