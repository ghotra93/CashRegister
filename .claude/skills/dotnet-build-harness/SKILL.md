---
name: dotnet-build-harness
description: The .NET 10 build and quality harness — `global.json` SDK pinning, `Directory.Build.props` shared settings, central package management, `.editorconfig` style truth, analyzers, the format gate, coverage collection, and the vulnerable-dependency gate. Use when wiring the harness into a new solution or bringing a brownfield repo up to it.
when_to_use:
  - Phase E setup (`/onboard` or `/wire-harness`) on a .NET solution.
  - Adding a missing harness layer to a brownfield repo.
  - Phase 6 (Validate) — mapping a failing gate back to the file that configures it.
  - Any PR that adds a `PackageReference`, a `NoWarn`, or a `#pragma warning disable`.
authoritative_references:
  - https://learn.microsoft.com/en-us/dotnet/core/tools/global-json
  - https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management
  - https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview
  - https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-code-coverage
  - .claude/skills/dotnet-coverage-and-mutation/SKILL.md
---

# .NET Build And Quality Harness

> Configuration lives in five files at the repo root. Projects carry almost nothing. Every gate is a command with a report path and a name in `artifacts/harness-summary.json`.

## Harness Layers

| Layer | Command | Report | Gate in `harness-summary.json` |
|---|---|---|---|
| Format | `dotnet format --verify-no-changes` | stdout (fails fast) | none — blocks before gates run |
| Compile + analyzers + style | `dotnet build -c Release` | stdout (warnings are errors) | none — blocks before gates run |
| Unit tests | `dotnet test --filter "Category!=Integration"` | `artifacts/test-results/unit.trx` | `unit` |
| Integration tests | `dotnet test --filter "Category=Integration"` | `artifacts/test-results/integration.trx` | `it` |
| Coverage | the same unit run, with `--coverage` | `artifacts/coverage/cobertura.xml` | `coverage` |
| Mutation (opt-in) | `dotnet stryker` | `artifacts/stryker/mutation-report.json` | `mutation` |
| Vulnerable dependencies | `dotnet list package --vulnerable --include-transitive` | `artifacts/vulnerable.txt` | none — warns, reported in `07-validation-report.md` |
| OpenAPI contract diff | generated document vs `origin/main` | `artifacts/openapi/openapi.json` | reported alongside the gates |

Format and build are **pre-gates**: they fail the run before any test executes, so a red gate always means a behaviour problem, never a style problem.

## The Canonical `dotnet test` Invocation

The full `dotnet test` command line — filters, coverage flags, TRX flags, output paths — is defined in exactly **two** places:

1. `.github/scripts/harness-dotnet.sh`
2. `references/props-fragments.md` in this skill

**Never duplicate it into a command file, a CI workflow step, a README, or a task description.** Command files invoke the script. A second copy drifts, and a drifted copy silently stops writing `artifacts/coverage/cobertura.xml`, which makes the `coverage` gate report a stale number.

- **Props fragments**: complete copy-paste `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `stryker-config.json`, and the exact `dotnet test` command line. Read [props-fragments.md](references/props-fragments.md)

## SDK Pinning — `global.json`

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature",
    "allowPrerelease": false
  }
}
```

`latestFeature` accepts `10.0.1xx` and `10.0.4xx` patches of the same feature band but refuses `11.x`. A machine without a matching SDK fails loudly instead of building against something else. Bumping the major band needs an ADR.

## Shared Settings — `Directory.Build.props`

One file at the repo root. Every project inherits it; no project repeats it.

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>

    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <CodeAnalysisTreatWarningsAsErrors>true</CodeAnalysisTreatWarningsAsErrors>

    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>

    <ArtifactsPath>$(MSBuildThisFileDirectory)artifacts</ArtifactsPath>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Meziantou.Analyzer" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

Why each one earns its place:

- `TreatWarningsAsErrors` + `EnforceCodeStyleInBuild` mean `dotnet build` **is** the analyzer gate and the style gate. There is no separate lint step to forget.
- `AnalysisLevel latest-recommended` turns on the current .NET analyzer recommendations; raise it to `latest-all` per project only with an ADR.
- `Deterministic` + `ContinuousIntegrationBuild` make the same source produce the same bytes, which is what lets the OpenAPI diff gate be meaningful.
- `ArtifactsPath` redirects all `bin/`/`obj/` output under `artifacts/`, so every report path in the table above is a single tree to publish and a single entry to gitignore.

## Central Package Management — `Directory.Packages.props`

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>

  <ItemGroup>
    <PackageVersion Include="Asp.Versioning.Http" Version="8.1.0" />
    <PackageVersion Include="FluentValidation" Version="12.0.0" />
    <PackageVersion Include="Mapster" Version="7.4.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Http.Resilience" Version="9.5.0" />
    <PackageVersion Include="xunit.v3" Version="1.1.0" />
  </ItemGroup>
</Project>
```

Project files then carry a bare reference — **no `Version` attribute, ever**:

```xml
<ItemGroup>
  <PackageReference Include="FluentValidation" />
  <PackageReference Include="Mapster" />
</ItemGroup>
```

| Situation | Where the version goes |
|---|---|
| Any package used by any project | `<PackageVersion>` in `Directory.Packages.props` |
| Analyzer applied solution-wide | `<PackageReference>` in `Directory.Build.props`, version in `Directory.Packages.props` |
| A single project genuinely needs a different version | `VersionOverride` on that `PackageReference`, plus an ADR |
| A dotnet CLI tool (Stryker, ef) | `.config/dotnet-tools.json` |

`CentralPackageTransitivePinningEnabled` promotes transitive dependencies to pinned versions, which is what makes `dotnet list package --vulnerable --include-transitive` actionable: you can fix an advisory by pinning, without waiting for the direct dependency to update.

## Style Truth — `.editorconfig`

`.editorconfig` is the single source of style truth. It drives `dotnet format`, the IDE, and — because `EnforceCodeStyleInBuild` is on — the compiler.

- Severity in `.editorconfig` is what makes a style rule a build error. `dotnet_diagnostic.IDE0055.severity = error` is the line that gives the format gate teeth.
- Encode the conventions the other skills mandate: file-scoped namespaces (`csharp_style_namespace_declarations = file_scoped:error`), `var` usage, expression bodies, `required` ordering, `sealed` preference.
- Analyzer severities for test projects are relaxed in a `[tests/**/*.cs]` section — in that file, not in a `.csproj`.

## Analyzers

| Package | Scope |
|---|---|
| .NET analyzers (built in) | `EnableNETAnalyzers` + `AnalysisLevel` |
| `Meziantou.Analyzer` | async correctness, `CancellationToken` plumbing, allocation and culture bugs the built-ins miss |

`Meziantou.Analyzer` is referenced once in `Directory.Build.props` with `PrivateAssets="all"` so it never flows into a package. Tune individual rules in `.editorconfig`, never by `NoWarn`.

## Format Gate

```bash
dotnet format --verify-no-changes --no-restore
```

Exits non-zero on the first unformatted file and prints the diff. The fix is always `dotnet format` followed by a commit — never a `.editorconfig` relaxation to match the code.

## Coverage Collection

Coverage comes from `Microsoft.Testing.Extensions.CodeCoverage`, emitted by the test executable itself:

```bash
dotnet test -c Release --no-build \
  --filter "Category!=Integration" \
  --coverage --coverage-output-format cobertura \
  --coverage-output "$(pwd)/artifacts/coverage/cobertura.xml"
```

There is no `coverlet` and no VSTest data collector in this stack. Thresholds, exclusions and report reading live in the `dotnet-coverage-and-mutation` skill.

## Dependency Gate

```bash
dotnet list package --vulnerable --include-transitive > artifacts/vulnerable.txt
```

Any `High` or `Critical` advisory is a blocker. Fix by bumping the `<PackageVersion>`; if no fixed version exists, record a waiver ADR naming the advisory id and the compensating control.

## Gitignore

```gitignore
bin/
obj/
artifacts/
*.user
```

`artifacts/` is build output and is never committed — **except** `artifacts/openapi/openapi.json`, which the contract diff gate compares against `origin/main` and which is therefore force-added:

```gitignore
!artifacts/openapi/
!artifacts/openapi/openapi.json
```

## Forbidden

- A `Version=` attribute on a `PackageReference` while `ManagePackageVersionsCentrally` is `true`.
- `NoWarn` used to silence an analyzer instead of fixing the code. Adjust severity in `.editorconfig` with a comment, or fix it.
- `#pragma warning disable` without a justifying comment naming the rule and the reason, on the line above.
- `#pragma warning disable` left un-restored to the end of the file.
- Per-project style or nullable settings that contradict `.editorconfig` or `Directory.Build.props` (`<Nullable>disable</Nullable>`, a local `TargetFramework`, a local `TreatWarningsAsErrors>false`).
- Committing `bin/`, `obj/`, or `artifacts/` (other than the generated OpenAPI document).
- Duplicating the canonical `dotnet test` command line outside `.github/scripts/harness-dotnet.sh` and `references/props-fragments.md`.
- An unpinned SDK (`global.json` absent, or `rollForward: latestMajor`).
- Lowering a coverage or mutation threshold to make a gate pass.
