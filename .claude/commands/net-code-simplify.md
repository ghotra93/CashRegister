---
description: Run /net-code-simplify — see .claude/commands/net-code-simplify.md for the authoritative spec.
argument-hint: see .claude/commands/net-code-simplify.md
agent: dotnet-implementer
---
# /net-code-simplify

**Phase:** 6 (meta) — clarity sweep
**Owning agent:** `.claude/agents/dotnet-implementer.md`
**Skills used:** `clarity-over-cleverness`, `tdd-red-green-refactor`, `dotnet-10-conventions`

## Purpose
Apply the clarity-over-cleverness rules to a single file or a directory while keeping tests green. This is the same pass `/net-build`'s simplify phase performs, but on demand.

## Inputs
- `<path>` (positional). Required. A file under `src/<Module>/**` or a directory.
- `--dry-run` (optional; prints proposed edits without writing).

## Reads
- `<path>` (the target file/directory).
- `.claude/skills/clarity-over-cleverness/SKILL.md` (authoritative checklist).
- `.specs/<feature-id>/01-spec.md` (the glossary, so renames use real domain names).

## Writes
- The targeted files (in place).
- A diff summary appended to `.specs/<feature-id>/05-implementation-log.md` under a `### simplify-pass` block, or to a fresh `clarity-pass-<date>.md` if no active feature.

## Process
1. Refuse if `dotnet test --filter "Category!=Integration"` is not green right now (run it first; abort on failure).
2. For each file in scope, apply the 7 rewrite targets in order:
   1. Untangle nested ternaries → `if`/`else` (and switch expressions only where they read better than a switch statement).
   2. LINQ only when clearer than a loop; otherwise loop.
   3. Inline once-used helpers.
   4. Kill option flags (split into two methods).
   5. Name domain concepts (no `Data`, `Info`, `Manager`, `Helper`, `Util`) using the `01-spec.md` glossary.
   6. Remove premature abstraction (an interface with one implementation, a factory with one product).
   7. Prefer early returns over deep nesting.
3. Extract every string or numeric literal that appears 2+ times in a file to a `const` or `static readonly` field.
4. Re-run `dotnet test --filter "Category!=Integration"` after each file, then `dotnet format --verify-no-changes` and `dotnet build -c Release` (warning-free). If anything goes red, revert that file's edits and record it as a Skipped item.
5. Emit a summary: files touched, rewrites applied per category, tests run, regressions encountered.

## Refuse if
- Tests are not green at start.
- The path is under `tests/**` (test clarity is governed by `/net-test`, not this command).
- A rewrite would change behavior, a public signature, or a test assertion — that is `/net-build` work.
- Clarity would be "achieved" by adding a `NoWarn` entry, a `#pragma warning disable`, or a `!` null-forgiving operator.
- A task is mid-flight in `.tdd-state.json` (finish `/net-build <task-id>` first).

## Done when
- All in-scope files are either simplified or explicitly skipped, with the skip reason recorded.
- `dotnet test --filter "Category!=Integration"` is green and `dotnet format --verify-no-changes` is clean.
- A `### simplify-pass` block is appended to the implementation log, and no commit was made automatically.
