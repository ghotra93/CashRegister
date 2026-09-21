---
description: Run /net-wire-harness — see .claude/commands/net-wire-harness.md for the authoritative spec.
argument-hint: see .claude/commands/net-wire-harness.md
agent: dotnet-onboarding
---
# /net-wire-harness

**Phase:** 0 (meta) — onboarding extension
**Owning agent:** `.claude/agents/dotnet-onboarding.md`
**Skills used:** `dotnet-build-harness`, `dotnet-coverage-and-mutation`, `efcore-10-data-access`, `harness-report-parsing`, `adr-authoring`

## Purpose
Wire the .NET harness layers (`global.json` SDK pin, `Directory.Build.props` shared settings, `Directory.Packages.props` central package management, `.editorconfig` style truth, Roslyn analyzers + Meziantou.Analyzer, the `dotnet format` gate, the unit/integration `Category` split, Cobertura coverage, opt-in Stryker.NET, and the `dotnet list package --vulnerable --include-transitive` gate) plus the EF Core migration tooling into a .NET solution. Runs against a solution already classified by `/net-onboard`, so subsequent `/net-spec` → `/net-plan` → `/net-build` cycles execute against fully gated quality bars from day one — without failing an existing build on day one.

This command exists because `/net-onboard` Process step 4 only *reports* missing harness layers, while `/net-build` is strictly TDD and refuses to edit project files without a `<task-id>` in flight. `/net-wire-harness` fills that gap.

## Inputs
None required. Optional argument: a path to a .NET solution or project directory (default `.`). For polyglot monorepos pass the .NET solution directory.

## Reads
- The target solution's `*.sln`/`*.slnx`, every `*.csproj`, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `.editorconfig`.
- `.specs/_stack.json` (must exist — produced by `/net-onboard`).
- `.specs/_onboarding.md` and `.specs/_baseline.json` (to know the current gate posture and what is already accepted as deferred).
- `.claude/skills/dotnet-build-harness/SKILL.md` (authoritative reference for every layer).
- `.claude/skills/dotnet-coverage-and-mutation/SKILL.md`, `.claude/skills/efcore-10-data-access/SKILL.md`, `.claude/skills/adr-authoring/SKILL.md`.

## Writes
- `Directory.Build.props` (shared settings: `TargetFramework`, `Nullable`, `TreatWarningsAsErrors`, analyzer enablement, deterministic build).
- `Directory.Packages.props` (central package management; every version lives here).
- `global.json` (SDK pin) and `.editorconfig` (style truth) if missing.
- The test projects' `*.csproj` (coverage collector `Microsoft.Testing.Extensions.CodeCoverage`, xUnit v3 on Microsoft.Testing.Platform).
- `stryker-config.json` — only when the user opts in to mutation testing.
- `.specs/_baseline.json` (refreshed via `./.github/scripts/harness-dotnet.sh --project <path> --baseline` after the wiring run).
- `.specs/_stack.json` (refreshed via `./.github/scripts/detect-stack-dotnet.sh <path> > .specs/_stack.json`).
- `.specs/_onboarding.md` (close the harness-debt findings; add an entry for any layer **deferred** during this pass with rationale and trigger).
- `.specs/adr/ADR-NNN-*.md` (one ADR per deferred layer that overrides a default in `dotnet-build-harness`, and one per ratcheted threshold).

## Process
1. **Pre-flight.** Refuse if `.specs/_stack.json` is missing (run `/net-onboard` first). Refuse if `migration == "both"`. If `migration == "none"` and the solution has a DB engine, refuse and instruct the user to record the EF Core migration decision as an ADR first.
2. **Compatibility check.** For each layer, verify the pinned analyzer/package version in `.claude/skills/dotnet-build-harness/SKILL.md` is compatible with the detected SDK and target framework from `.specs/_stack.json`. If a layer is incompatible, **defer it** — do not invent a workaround. Each deferral becomes an ADR in step 6.
3. **Wire layers.** Edit the build files per `.claude/skills/dotnet-build-harness/SKILL.md`:
   - `global.json` pinning the SDK; `Directory.Build.props` with `Nullable`, `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, `AnalysisLevel`.
   - `Directory.Packages.props` with `ManagePackageVersionsCentrally` and every version; project files carry bare `<PackageReference Include="..." />`.
   - `.editorconfig` as the single style truth backing `dotnet format --verify-no-changes`.
   - Roslyn analyzers plus Meziantou.Analyzer as the static-analysis layer.
   - Coverage via `Microsoft.Testing.Extensions.CodeCoverage` emitting `artifacts/coverage/cobertura.xml`, with the 0.90 line+branch floor (full floor for greenfield; ratchet from `.specs/_baseline.json` for brownfield). Exclude generated code and the host entry point.
   - The unit/integration split on `[Trait("Category", "Integration")]`, so `dotnet test --filter "Category!=Integration"` is the `unit` gate and `dotnet test --filter "Category=Integration"` is the `it` gate.
   - Stryker.NET **opt-in only**: leave `stryker-config.json` absent unless the user asks for it, so the `mutation` gate reports `skipped`. `skipped` is not a failure.
   - The vulnerable-dependency gate via `dotnet list package --vulnerable --include-transitive`.
   - EF Core migration tooling so `dotnet ef migrations add` and `dotnet ef migrations script --idempotent` work; migrations are never applied at startup.
4. **Config files.** Create `.editorconfig` and `global.json` if absent. Fix obvious project-file hygiene that the format gate will flag (empty template-generated properties, stray `Version` attributes that belong in `Directory.Packages.props`).
5. **Apply formatting.** Run `dotnet format` once to bring existing source files into compliance. This is the **only** edit to `src/**` or `tests/**` allowed in this command, and it is mechanical.
6. **Document deferrals.** For each layer deferred in step 2, write `.specs/adr/ADR-NNN-<slug>.md` per `.claude/skills/adr-authoring/SKILL.md` (Context, Decision drivers, Considered options, Decision outcome with rationale, Consequences, Trigger to revisit). Record the matching debt entry in `.specs/_onboarding.md`.
7. **Verify.** Run `dotnet format --verify-no-changes`, `dotnet build -c Release`, and `dotnet test --filter "Category!=Integration"`. The build must be **green and warning-free**. Then run `./.github/scripts/harness-dotnet.sh --project <path> --baseline` and capture the result into `.specs/_baseline.json`. If a pre-existing failure cannot be made green without touching `src/**`, record it as the ratchet baseline instead of fixing it here.
8. **Refresh artifacts.** Re-run `./.github/scripts/detect-stack-dotnet.sh <path> > .specs/_stack.json`. Update `.specs/_onboarding.md` to mark the harness-layers debt resolved (preserve the finding, prepend the resolution note) and to record the chosen thresholds, code style, and unit/integration test-naming conventions.

## Refuse if
- `.specs/_stack.json` does not exist (precondition: run `/net-onboard` first).
- No `.sln`, `.slnx`, or `.csproj` is found at the resolved path.
- `migration == "both"` in the stack JSON (fatal; same rule as `/net-onboard`).
- `migration == "none"` and the solution has a DB engine and no ADR has recorded the migration approach.
- The agent would need to edit any file under `src/**` or `tests/**` other than via `dotnet format` (test code and architecture-rule code belong to a future `/net-build` task).
- Any harness layer's compatibility cannot be established and the user has not chosen between deferring it or pinning an explicit override version.
- A coverage or mutation threshold would need to be lowered without a `.specs/_baseline.json` baseline plus an ADR justifying the ratchet.
- Enabling a gate would require a skip flag or `-p:TreatWarningsAsErrors=false` to keep the build green (`forbid-skip-flags.sh` blocks it) — defer the layer with an ADR instead.

## Done when
- `dotnet format --verify-no-changes`, `dotnet build -c Release`, and `dotnet test --filter "Category!=Integration"` are green from a clean checkout (after the one-shot `dotnet format`).
- `./.github/scripts/harness-dotnet.sh --project <path> --report > artifacts/harness-summary.json` shows every wired layer as `pass`, or `skipped` where it is opt-in (`mutation` with no `stryker-config.json`).
- `.specs/_baseline.json` exists and reflects the post-wiring run, with any pre-existing failure recorded as the ratchet baseline rather than fixed.
- `.specs/_onboarding.md` no longer lists the harness-layers debt as open; any deferred layer has its own debt entry plus an ADR.
- The user has been told the next recommended command is `/net-spec` (greenfield) or `/net-build <task-id>` (if a task is already pending).
