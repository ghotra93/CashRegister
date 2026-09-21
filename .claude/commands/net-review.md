---
description: Run /net-review — see .claude/commands/net-review.md for the authoritative spec.
argument-hint: see .claude/commands/net-review.md
agent: dotnet-code-reviewer
---
# /net-review

**Phase:** 6 — pre-commit code review
**Owning agent:** `.claude/agents/dotnet-code-reviewer.md`
**Skills used:** `dotnet-code-review-rubric`, `clarity-over-cleverness`, `dotnet-10-conventions`, `dotnet-security-baseline`, `efcore-10-data-access`, `dotnet-observability-ops`

## Purpose
Run a structured self-review of the diff before the user commits. Produces `08-code-review.md` with categorized findings.

## Inputs
- `<feature-id>` (optional; defaults to the most recent feature).
- `--base <ref>` (optional; defaults to `origin/main`).

## Reads
- `git diff <base>...HEAD` for changed `*.cs`, test, `*.csproj`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, EF Core migration and OpenAPI files.
- `.specs/<feature-id>/01-spec.md`, `03-design.md`, `07-validation-report.md`, `07a-traceability.md`
- `.claude/skills/dotnet-code-review-rubric/SKILL.md` (the rubric is authoritative)
- `.claude/templates/code-review.template.md`

## Writes
- `.specs/<feature-id>/08-code-review.md`

## Process
1. Refuse if `07-validation-report.md` verdict is not `PASS`.
2. Walk every changed file. Evaluate each against the rubric categories: traceability, slice and module boundaries, Minimal API idioms (`TypedResults`, endpoint filters, FluentValidation), error handling (`ProblemDetails` only, no `ex.Message` to clients), EF Core data access (no startup migrations, `AsNoTracking` projections, mandatory capped pagination, no N+1), nullable discipline, async correctness (`CancellationToken` threaded, no sync-over-async, no `async void`), security, observability, test quality, clarity, conventions, packaging.
3. Classify findings as `must-fix`, `should-fix`, `nit`, or `praise`. Include file + line range + suggested change for `must-fix` and `should-fix`.
4. Cross-check: every AC in `01-spec.md` is exercised by at least one test carrying `[Trait("AC", "AC-NNN")]` in the diff (or already merged), and every integration test in the diff also carries `[Trait("Category", "Integration")]`.
5. Flag any added `NoWarn` entry, `#pragma warning disable` without a reason comment, `!` null-forgiving operator without justification, or inline `Version` attribute on a `PackageReference` (versions belong in `Directory.Packages.props`).
6. If any `must-fix` exists, recommend `/net-build` or `/net-code-simplify`. Do NOT auto-apply fixes.
7. Emit summary line: counts by severity + recommended next action.

## Refuse if
- `07-validation-report.md` is missing or its verdict is not `PASS`.
- The diff is empty.
- Any `Q-NNN` is unresolved in the spec or design.
- A finding is accepted without fixing it and has no covering ADR.

## Done when
- `08-code-review.md` exists with every rubric category answered and a verdict of `Approve`, `Approve with waivers`, or `Request changes`.
- If zero `must-fix`, the agent prints the suggested commit message and tells the user to run `git commit` themselves (the agent never commits).
- If `must-fix` findings remain, the smallest recovery command is named.
