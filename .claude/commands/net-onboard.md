---
description: Run /net-onboard — see .claude/commands/net-onboard.md for the authoritative spec.
argument-hint: see .claude/commands/net-onboard.md
agent: dotnet-onboarding
---
# /net-onboard

**Phase:** 0 — bootstrap
**Owning agent:** `.claude/agents/dotnet-onboarding.md`
**Skills used:** `brownfield-onboarding`, `dotnet-build-harness`, `efcore-10-data-access`, `harness-report-parsing`, `dotnet-architecture-rules`

## Purpose
Inspect the repository, classify it as greenfield or brownfield, capture a baseline harness run, and produce `.specs/_onboarding.md` so future commands know the stack. Day one never fails an existing build — the baseline is recorded and then ratcheted.

## Inputs
None required. Optional argument: a path to constrain the scan (a `.sln`/`.slnx`, a `.csproj`, or the directory holding one).

- For a **single-solution** repo: omit the argument; the script resolves the `.sln`/`.slnx` at the repo root.
- For a **multi-project solution**: pass the project or solution directory to scope the scan.
- For a **polyglot monorepo** (e.g. an ASP.NET Core backend plus an Angular/React frontend in sibling top-level dirs): pass the .NET solution directory. Sibling non-.NET apps are auto-detected and recorded under `siblings` in `.specs/_stack.json` and as context-only notes in `.specs/_onboarding.md`. The harness only validates the .NET solution; frontend tooling is owned by its own pipeline.

## Reads
- `*.sln` / `*.slnx` / `*.csproj`, plus `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `.editorconfig`
- `src/**` and `tests/**` (counts only)
- EF Core `**/Migrations/**`
- `stryker-config.json` (present or absent — decides whether the `mutation` gate can run)
- Existing `.specs/` if any

## Writes
- `.specs/_onboarding.md`
- `.specs/_stack.json` (output of `./.github/scripts/detect-stack-dotnet.sh`)
- `.specs/_baseline.json` (only if any test exists; output of `./.github/scripts/harness-dotnet.sh --baseline`)

## Process
1. Resolve the solution/project path (`PROJECT` = optional arg, else `.`). Run `./.github/scripts/detect-stack-dotnet.sh <path> > .specs/_stack.json`. The script exits 1 when no `.sln`/`.slnx`/`.csproj` is found. Refuse to proceed if `migration == "both"`.
2. Count source/test files under `src/` and `tests/`; classify:
   - **Greenfield** if there is no `src/<Module>/**/*.cs` beyond the generated host (`Program.cs`) — auto-generated smoke tests and Aspire/Testcontainers scaffolding do not change the classification.
   - **Brownfield** otherwise.
3. If brownfield, run `./.github/scripts/harness-dotnet.sh --project <path> --baseline` and capture results into `.specs/_baseline.json`. Do NOT attempt to fix failures — a red gate on day one becomes a recorded baseline, not a blocker.
4. Compare the solution's harness surface against `.claude/skills/dotnet-build-harness/SKILL.md` (pinned SDK in `global.json`, shared settings in `Directory.Build.props`, central package management in `Directory.Packages.props`, `.editorconfig` style truth, Roslyn + Meziantou.Analyzer, the `dotnet format` gate, Cobertura coverage collection, the `dotnet list package --vulnerable` gate). List every missing layer as a Finding.
5. Record the gate posture: `unit`, `it`, `coverage`, `mutation`. Note that with no `stryker-config.json` the `mutation` gate reports `skipped`, which is not a failure.
6. Write `.specs/_onboarding.md` covering: classification, solution/project path, module list, sibling apps (if any), stack JSON summary, baseline gate results (or "N/A — greenfield"), missing harness layers, current coverage vs the 0.90 line+branch floor as a ratchet target, and the recommended starting point.

## Refuse if
- No `.sln`, `.slnx`, or `.csproj` is found at the resolved path (in a polyglot monorepo the user must pass the .NET solution directory).
- `migration == "both"` — fatal; ask the user to pick one migration approach.
- The only way to make the baseline green would be to edit `src/**` or `tests/**` — onboarding records reality, it does not fix it.

## Done when
- `.specs/_onboarding.md`, `.specs/_stack.json`, and (if any test exists) `.specs/_baseline.json` exist.
- No existing build or gate was "fixed" to make the baseline look green.
- The user has been shown a one-paragraph summary plus the next recommended command — `/net-wire-harness` if layers are missing, otherwise `/net-spec`.
