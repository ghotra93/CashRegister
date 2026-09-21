# Copy-Paste Harness Fragments

Every fragment goes at the **repo root** unless stated otherwise. Drop them in whole; do not cherry-pick properties.

## `global.json`

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature",
    "allowPrerelease": false
  },
  "msbuild-sdks": {
    "Microsoft.Build.Traversal": "4.1.0"
  }
}
```

## `Directory.Build.props`

```xml
<Project>

  <PropertyGroup Label="Language">
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>

  <PropertyGroup Label="Quality">
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningLevel>9999</WarningLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <AnalysisMode>Recommended</AnalysisMode>
    <CodeAnalysisTreatWarningsAsErrors>true</CodeAnalysisTreatWarningsAsErrors>
    <EnableNuGetAuditMode>all</EnableNuGetAuditMode>
    <NuGetAuditLevel>high</NuGetAuditLevel>
  </PropertyGroup>

  <PropertyGroup Label="Reproducibility">
    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
    <DebugType>portable</DebugType>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <ArtifactsPath>$(MSBuildThisFileDirectory)artifacts</ArtifactsPath>
  </PropertyGroup>

  <ItemGroup Label="Solution-wide analyzers">
    <PackageReference Include="Meziantou.Analyzer" PrivateAssets="all" />
  </ItemGroup>

</Project>
```

Test-project overrides go in `tests/Directory.Build.props`, which inherits the root file:

```xml
<Project>
  <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />

  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <OutputType>Exe</OutputType>
    <UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>
    <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="Microsoft.Testing.Extensions.CodeCoverage" />
    <PackageReference Include="NSubstitute" />
    <PackageReference Include="Shouldly" />
    <PackageReference Include="Verify.XUnit" />
    <PackageReference Include="Microsoft.Extensions.TimeProvider.Testing" />
  </ItemGroup>
</Project>
```

Forgetting the `Import` silently drops nullable, analyzers and warnings-as-errors for the whole test tree.

## `Directory.Packages.props`

```xml
<Project>

  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>

  <ItemGroup Label="Web">
    <PackageVersion Include="Asp.Versioning.Http" Version="8.1.0" />
    <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="10.0.0" />
    <PackageVersion Include="Scalar.AspNetCore" Version="2.0.0" />
  </ItemGroup>

  <ItemGroup Label="Application">
    <PackageVersion Include="FluentValidation" Version="12.0.0" />
    <PackageVersion Include="FluentValidation.DependencyInjectionExtensions" Version="12.0.0" />
    <PackageVersion Include="Mapster" Version="7.4.0" />
    <PackageVersion Include="Mapster.DependencyInjection" Version="1.0.1" />
  </ItemGroup>

  <ItemGroup Label="Data">
    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.0" />
    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0" />
  </ItemGroup>

  <ItemGroup Label="Resilience and telemetry">
    <PackageVersion Include="Microsoft.Extensions.Http.Resilience" Version="9.5.0" />
    <PackageVersion Include="OpenTelemetry.Extensions.Hosting" Version="1.10.0" />
  </ItemGroup>

  <ItemGroup Label="Analyzers">
    <PackageVersion Include="Meziantou.Analyzer" Version="2.0.182" />
  </ItemGroup>

  <ItemGroup Label="Test">
    <PackageVersion Include="xunit.v3" Version="1.1.0" />
    <PackageVersion Include="Microsoft.Testing.Extensions.CodeCoverage" Version="17.14.4" />
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.0" />
    <PackageVersion Include="Microsoft.Extensions.TimeProvider.Testing" Version="9.5.0" />
    <PackageVersion Include="NSubstitute" Version="5.3.0" />
    <PackageVersion Include="Shouldly" Version="4.3.0" />
    <PackageVersion Include="Verify.XUnit" Version="28.9.0" />
    <PackageVersion Include="Testcontainers.PostgreSql" Version="4.3.0" />
    <PackageVersion Include="Respawn" Version="6.2.1" />
    <PackageVersion Include="TngTech.ArchUnitNET.xUnitV3" Version="0.11.0" />
  </ItemGroup>

</Project>
```

Consuming project — note the absent versions:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="FluentValidation" />
    <PackageReference Include="Mapster" />
    <PackageReference Include="Microsoft.EntityFrameworkCore" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
  </ItemGroup>
</Project>
```

## `.editorconfig`

```ini
root = true

[*]
charset = utf-8
end_of_line = lf
insert_final_newline = true
trim_trailing_whitespace = true
indent_style = space
indent_size = 2

[*.{csproj,props,targets,xml,json,yml,yaml}]
indent_size = 2

[*.cs]
indent_size = 4
max_line_length = 120

# --- formatting: IDE0055 is what gives `dotnet format` teeth ---
dotnet_diagnostic.IDE0055.severity = error

csharp_using_directive_placement = outside_namespace:error
dotnet_sort_system_directives_first = true
dotnet_separate_import_directive_groups = false
csharp_new_line_before_open_brace = all
csharp_prefer_braces = true:error

# --- mandated language style ---
csharp_style_namespace_declarations = file_scoped:error
csharp_style_prefer_primary_constructors = true:warning
csharp_style_expression_bodied_methods = when_on_single_line:suggestion
csharp_style_prefer_pattern_matching = true:warning
csharp_style_prefer_switch_expression = true:warning
csharp_prefer_simple_using_statement = true:warning
dotnet_style_prefer_collection_expression = true:warning
dotnet_style_readonly_field = true:error
dotnet_style_qualification_for_field = false:error
csharp_style_var_for_built_in_types = false:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion

# --- nullable and async discipline ---
dotnet_diagnostic.CS8600.severity = error
dotnet_diagnostic.CS8602.severity = error
dotnet_diagnostic.CS8618.severity = error
dotnet_diagnostic.CA2007.severity = none          # ConfigureAwait unnecessary in ASP.NET Core
dotnet_diagnostic.CA1848.severity = warning       # prefer LoggerMessage delegates
dotnet_diagnostic.CA2016.severity = error         # forward CancellationToken
dotnet_diagnostic.MA0004.severity = none          # Meziantou ConfigureAwait, same reason as CA2007
dotnet_diagnostic.MA0040.severity = error         # pass a CancellationToken
dotnet_diagnostic.MA0045.severity = error         # do not use blocking calls in async

# --- naming: interfaces ---
dotnet_naming_rule.interfaces_start_with_i.severity = error
dotnet_naming_rule.interfaces_start_with_i.symbols = interfaces
dotnet_naming_rule.interfaces_start_with_i.style = prefix_i
dotnet_naming_symbols.interfaces.applicable_kinds = interface
dotnet_naming_style.prefix_i.required_prefix = I
dotnet_naming_style.prefix_i.capitalization = pascal_case

# --- private fields: camelCase, no underscore ---
dotnet_naming_rule.private_fields_camel.severity = error
dotnet_naming_rule.private_fields_camel.symbols = private_fields
dotnet_naming_rule.private_fields_camel.style = camel
dotnet_naming_symbols.private_fields.applicable_kinds = field
dotnet_naming_symbols.private_fields.applicable_accessibilities = private
dotnet_naming_style.camel.capitalization = camel_case

[tests/**/*.cs]
dotnet_diagnostic.CA1707.severity = none          # Method_Scenario_Expected names use underscores
dotnet_diagnostic.CA1861.severity = none          # constant arrays in InlineData are fine
dotnet_diagnostic.MA0004.severity = none
```

Test relaxations live here, in a path-scoped section — never as properties in a `.csproj`.

## `stryker-config.json` (opt-in — its presence enables the mutation gate)

```json
{
  "stryker-config": {
    "project": "src/Ordering/Ordering.csproj",
    "test-projects": ["tests/Ordering.Tests/Ordering.Tests.csproj"],
    "since": { "target": "origin/main", "enabled": true },
    "thresholds": { "high": 85, "low": 75, "break": 75 },
    "reporters": ["json", "html", "progress"],
    "mutate": ["src/Ordering/Features/**/*.cs"],
    "ignore-mutations": ["Logging"]
  }
}
```

The Testcontainers project is deliberately absent from `test-projects`.

## `.config/dotnet-tools.json`

```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "dotnet-ef": { "version": "10.0.0", "commands": ["dotnet-ef"] },
    "dotnet-stryker": { "version": "4.6.0", "commands": ["dotnet-stryker"] },
    "dotnet-outdated-tool": { "version": "4.6.8", "commands": ["dotnet-outdated"] }
  }
}
```

`dotnet tool restore` before any tool invocation. Tool versions never live in `Directory.Packages.props`.

## The canonical `dotnet test` command line

This and `.github/scripts/harness-dotnet.sh` are the **only** two places this command may appear.

```bash
# unit gate + coverage gate (one run, two reports)
dotnet test -c Release --no-build \
  --filter "Category!=Integration" \
  --coverage \
  --coverage-output-format cobertura \
  --coverage-output "$(pwd)/artifacts/coverage/cobertura.xml" \
  --report-trx --report-trx-filename "unit.trx" \
  --results-directory "$(pwd)/artifacts/test-results"

# it gate (no coverage — container-backed runs would skew the numbers)
dotnet test -c Release --no-build \
  --filter "Category=Integration" \
  --report-trx --report-trx-filename "integration.trx" \
  --results-directory "$(pwd)/artifacts/test-results"
```

Notes that are load-bearing:

- `--coverage-output` must be **absolute**; a relative path resolves against each test project's own output directory and you get N partial reports instead of one.
- `--no-build` requires a preceding `dotnet build -c Release`. Without it the analyzer gate runs twice and the run doubles in length.
- The `--filter` values must match `[Trait("Category", "Integration")]` exactly. A test class missing that trait lands in the unit gate.
- Coverage is collected on the unit run only. The `it` run deliberately has no `--coverage`.

## Full local run

```bash
dotnet tool restore
dotnet restore
dotnet format --verify-no-changes --no-restore
dotnet build -c Release
./.github/scripts/harness-dotnet.sh          # runs both test gates and writes harness-summary.json
dotnet list package --vulnerable --include-transitive
```
