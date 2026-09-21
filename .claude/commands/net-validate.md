---
description: Run /net-validate — see .claude/commands/net-validate.md for the authoritative spec.
argument-hint: see .claude/commands/net-validate.md
agent: dotnet-validator
---
# /net-validate

**Phase:** 5 — validate (run the harness)
**Owning agent:** `.claude/agents/dotnet-validator.md`
**Skills used:** `harness-report-parsing`, `dotnet-coverage-and-mutation`, `requirements-traceability`, `dotnet-architecture-rules`, `dotnet-build-harness`

## Purpose
Run the full .NET harness, parse the output, and write `07-validation-report.md` with a single `PASS` / `FAIL` verdict.

## Inputs
- `<feature-id>` (optional; if omitted, reports across all changes since `origin/main`).

## Reads
- `./.github/scripts/harness-dotnet.sh`, `./.github/scripts/check-new-code-coverage-dotnet.sh`, `./.github/scripts/traceability-dotnet.sh`
- `artifacts/harness-summary.json`, `artifacts/coverage/cobertura.xml`, `artifacts/new-code-coverage.json`, `artifacts/openapi/openapi.json` (after the run)
- `.specs/_stack.json` (which layers are configured), `.specs/_baseline.json` (brownfield deltas)
- `.specs/<feature-id>/01-spec.md`, `04-tasks.md`, `06-test-plan.md` (for AC mapping)
- `.claude/templates/validation-report.template.md`, `.claude/checklists/validation-gates.md`

## Writes
- `.specs/<feature-id>/07-validation-report.md`
- `.specs/<feature-id>/07a-traceability.md` (regenerated)
- `artifacts/harness-summary.json`, `artifacts/new-code-coverage.json`

## Process
1. Run `./.github/scripts/harness-dotnet.sh --report > artifacts/harness-summary.json`. Layers: format (`dotnet format --verify-no-changes`) → build (`dotnet build -c Release` with Roslyn analyzers + Meziantou.Analyzer, warnings are errors) → unit (`dotnet test --filter "Category!=Integration"`) → integration (`dotnet test --filter "Category=Integration"`) → coverage (Cobertura) → mutation (Stryker.NET, opt-in) → contract (OpenAPI diff) → vulnerable dependencies (`dotnet list package --vulnerable --include-transitive`).
2. Run `./.github/scripts/check-new-code-coverage-dotnet.sh` (env `NEW_CODE_THRESHOLD` default `0.95`, `BASE_REF` default `origin/main`). Must be ≥ 95% on changed lines.
3. Run `./.github/scripts/traceability-dotnet.sh <feature-id>`. Any AC with zero tests = FAIL. An AC whose only test lacks `[Trait("AC", ...)]` reads as uncovered — report it as a missing tag, not as missing behavior.
4. Aggregate the gates — `unit`, `it`, `coverage`, `mutation` — plus new-code coverage and traceability. Coverage floor is 0.90 line and branch overall, 0.95 on new code. Verdict is `PASS` only if every gate is `pass` or `skipped`; a `skipped` mutation gate (no `stryker-config.json`) is expected and is **not** a failure. A `Survived` mutant in changed code fails unless ADR-justified.
5. Diff every metric against `.specs/_baseline.json`. Anything that worsened is a finding, severity per `.claude/checklists/validation-gates.md`.
6. Write `07-validation-report.md` with: verdict, gate table, coverage detail (overall, per-project, new code), mutation detail or the reason the gate is `skipped`, OpenAPI contract diff (breaking / non-breaking) against `artifacts/openapi/openapi.json`, vulnerable-dependency advisories and waivers, baseline diff, top failing items, artifact links, recommended next action.

## Refuse if
- `04-tasks.md` shows any task not `done`.
- The harness was bypassed or a cached result was reused — always re-run.
- A skip or threshold-lowering flag would be needed (`forbid-skip-flags.sh` blocks the underlying `dotnet` invocation; `COVERAGE_FLOOR`, `NEW_CODE_THRESHOLD` and `MUTATION_THRESHOLD` are never lowered).
- A report is missing for a layer `_stack.json` says is configured — that is an `error`, not a `pass`.
- Any `Q-NNN` is unresolved in the spec or design (`block-progress-on-open-questions.sh`).
- A waiver has no covering ADR.

## Done when
- `07-validation-report.md` and `07a-traceability.md` exist with a clear verdict.
- Every gate is accounted for, including the reason for any `skipped` layer.
- If `PASS`, the user is pointed to `/net-review`. If `FAIL`, the report lists the smallest set of `/net-build` or `/net-test` actions needed to recover.
