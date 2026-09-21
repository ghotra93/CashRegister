# Implementation Log: 2026-09-21-cash-register-change-calculation

> One `### <task-id> — <phase>` block per TDD phase, appended by `/net-build`.

### T-001 — red
- Date: 2026-09-21
- Tests written: `tests/CashRegister.Tests/ScaffoldSmokeTests.cs` (T-001-T1), `tests/CashRegister.IntegrationTests/ScaffoldSmokeTests.cs` (T-001-T2)
- Also added (artifacts): `global.json` (MTP runner), `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `.gitignore`, `CashRegister.slnx`, both test `.csproj` files (xUnit v3 4.0.1 on Microsoft.Testing.Platform).
- Run: `dotnet test --solution CashRegister.slnx` → 2 failed, 0 passed.
- Failure (assertion, not compile):
  - T1: `referenced should contain "CashRegister" but did not`
  - T2: `program should not be null but was`
- Harness note: fixed the Windows backslash-path bug in five `.claude/hooks/*.sh` scripts (normalise `\` → `/`), with the user's approval.
- Amendment (still red): T1 switched from `GetReferencedAssemblies()` to `Assembly.Load("CashRegister")`. The module has no types yet, so the compiler would drop an unused reference and T1 could never go green. Re-run: `load() should not throw but threw`.

### T-001 — green
- Production/config added: `src/CashRegister/CashRegister.csproj` (FrameworkReference `Microsoft.AspNetCore.App`; InternalsVisibleTo both test projects + DynamicProxyGenAssembly2), `src/CashRegister.Api/CashRegister.Api.csproj`, `Program.cs` (minimal host + `public partial class Program;`), `appsettings.json`, `appsettings.Development.json`, `Properties/launchSettings.json` (http://localhost:5080).
- Test projects reference the module and the host respectively; all four projects added to `CashRegister.slnx`.
- Run: `dotnet test --solution CashRegister.slnx` → 2 passed, 0 failed.

### T-001 — refactor
- No structural changes needed: two small test files and project files only. Suite re-run green.

### T-001 — simplify
- Reviewed against clarity-over-cleverness; nothing to simplify. A comment in T1 explains why the assembly is loaded by name.
- `dotnet format CashRegister.slnx --verify-no-changes` → exit 0.
- `dotnet build CashRegister.slnx -c Release` → 0 warnings, 0 errors.
- Unit (`--filter-not-trait Category=Integration`): 1/1 passed. Integration (`--filter-trait Category=Integration`): 1/1 passed.
- Status: **done**.
