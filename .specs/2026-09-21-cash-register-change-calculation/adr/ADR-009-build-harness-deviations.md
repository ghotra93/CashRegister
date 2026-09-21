# ADR-009: Build-harness defaults this repo overrides

- **Status:** accepted
- **Date:** 2026-09-21
- **Deciders:** user, `dotnet-architect`
- **Consulted:** `dotnet-build-harness` skill

## Context and problem statement

The harness was wired from the `dotnet-build-harness` skill. Four of its defaults conflict with choices already made here, or with the repo's own guard rails. The user decided each one on 2026-09-21.

## Decisions

| # | Harness default | This repo | Why |
|---|---|---|---|
| 1 | Coverage is measured on the **unit** run only | **Kept (canonical).** The endpoints are proved only by integration tests today, so unit-only coverage is 73.2% line / 75.4% branch and the gate fails. **T-017** adds endpoint unit tests to reach the 90% floor. Thresholds are not lowered. | User choice: "unit only, add unit tests". |
| 2 | `Meziantou.Analyzer` + `GenerateDocumentationFile` | **Waived.** The .NET analyzers stay at `latest-recommended` with warnings (and analyzer warnings) as errors. | User choice. A trial build showed about 36 findings (28 missing XML docs, 5 one-type-per-file, 3 ConfigureAwait, which the skill's own `.editorconfig` disables). |
| 3 | Private fields `camelCase`, no underscore | **`_camelCase` private fields**; PascalCase types, members, constants and static readonly fields; `I`-prefixed interfaces; no Hungarian notation. Enforced as errors in `.editorconfig`. | User: "c# is PascalCase, private fields can use _camelCase no hungarian notation". Matches the existing code and the .NET runtime style. |
| 4 | `dotnet test --no-build` after `dotnet build` | **No `--no-build`.** The script builds once, then `dotnet test -c Release` does an incremental no-op rebuild. | The repo guard (`settings.json` deny rule + `forbid-skip-flags.sh`) forbids `--no-build` as a stale-build risk; the user chose to keep the guard. |

## Smaller adaptations (no user decision needed)

- **Test filters:** xUnit v3 on Microsoft.Testing.Platform uses `--filter-trait` / `--filter-not-trait`, not VSTest's `--filter`. The trait `Category=Integration` is unchanged.
- **Per-project test runs:** the unit gate runs once per test project. A solution-wide run with one `--coverage-output` path lets the last process overwrite the others, which was observed. Each project writes its own Cobertura file, and `lib/cobertura.mjs` merges them (a line is covered if any run hit it) into `artifacts/coverage/cobertura.xml`. `--ignore-exit-code 8` tolerates a project with no tests for that gate; the gate still fails if the total is zero.
- **Contract gate:** no `Microsoft.Extensions.ApiDescription.Server` package. The script starts the built API in Development, fetches `/openapi/v1.json` into `artifacts/openapi/openapi.json` (committed; see `.gitignore`), and diffs operations (path + method) against `BASE_REF`. Schema-level changes are not diffed.
- **Not adopted:** `ArtifactsPath` (moves `bin/`/`obj/`, and no report path depends on it), `InvariantGlobalization`, the `Microsoft.Build.Traversal` SDK, `.config/dotnet-tools.json` (no dotnet tools are used; Stryker is not opted in), and `Verify.XUnit` (dropped as Gap-004 because of its licence requirement).
- **Web gates** (lint, typecheck, Vitest coverage with a 90% floor, production build) and `npm audit` (high/critical blocking) run inside the same harness, because the repo ships a React app.

## Consequences

- Positive: one command (`./.github/scripts/harness-dotnet.sh --report`) produces every report `/net-validate` reads.
- Negative: until T-017 is done, the `coverage` gate is red and the overall harness verdict is `fail`.

## Links

- Skill: `.claude/skills/dotnet-build-harness/SKILL.md`, `references/props-fragments.md`
- Related: ADR-005 (security), ADR-008 (observability), `06-test-plan.md` Gap-004 / Gap-007
