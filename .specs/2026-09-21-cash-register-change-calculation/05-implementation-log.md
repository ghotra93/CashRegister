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

### T-002 — red
- Tests: `DenominationTests` (AC-009, AC-010), `UsdCurrencyTests` (AC-007, AC-029), `CurrencyRegistryTests`. 21 test cases across T-002-T1..T6.
- Stage 1: compile failure (CS0246, types missing), recorded so the hook would allow skeleton production files.
- Stage 2: skeleton types (empty values / `NotImplementedException`) added. Run `dotnet test --project tests/CashRegister.Tests` → 21 failed, 1 passed (T-001 smoke). Examples:
  - `_usd.Code should be "USD" but was ""`
  - `Constructor_WithDuplicateValues_Throws: should throw ArgumentException but did not`

### T-002 — green
- Implemented `Denomination` (validation, `NameFor`), abstract `Currency` (validates non-empty, unique values, has a 1-unit denomination; stores denominations largest first), `UsdCurrency` (USD, `.`, 2 digits, dollar/quarter/dime/nickel/penny), `ICurrencyRegistry` / `CurrencyRegistry` (case-insensitive, duplicate code throws).
- Run: unit suite 22/22 passed.

### T-002 — refactor
- No structural changes needed. The stub-then-implement cycle left no dead code. Suite green.

### T-002 — simplify
- Used named arguments in `UsdCurrency` for readability, and a `largestFirst` local name in `Currency`.
- `.editorconfig`: added a PascalCase naming rule for private static readonly fields. The underscore rule had flagged `DenominationTests.Penny` (IDE1006).
- `dotnet format --verify-no-changes` → exit 0; `dotnet build -c Release` → 0 warnings; full suite 23/23 passed (unit + integration).
- Coverage (Cobertura, `artifacts/coverage/cov.xml`): the Currencies slice has 100% line and 100% branch coverage.
- Status: **done**.

### T-003 — red
- Tests: `TransactionTests` (AC-005 + validation), `MinimalChangeStrategyTests` (AC-003: 88¢, 3¢, 167¢; AC-005: sums exactly for 0..1000¢; zero → empty; negative throws).
- Stage 1: compile failure (types missing). Stage 2: skeletons. They had to read instance data because CA1822 fails the build as a warning-as-error.
- Run → 9 failed / 24 passed. The 2 new edge cases pass trivially against the stubs: exact payment gives 0, and 0 gives an empty list. Example: `MakeChange_88Cents … should be [(quarter,3),(dime,1),(penny,3)] but was []`.

### T-003 — green
- `Transaction`: validates non-negative amounts and paid ≥ owed; `ChangeDue = Paid - Owed`.
- `ChangeLine.Total = value × count`. `IChangeStrategy.MakeChange(amount, currency)`.
- `MinimalChangeStrategy`: greedy, largest first; skips zero counts.
- Unit suite 33/33 passed.

### T-003 — refactor
- The greedy loop subtracts `line.Total`, so the value × count arithmetic lives only in `ChangeLine`. Suite green.

### T-003 — simplify
- Early `continue` for zero counts instead of a nested `if`. Nothing else to simplify.
- `dotnet format --verify-no-changes` → 0; Release build → 0 warnings; full suite 34/34 passed.
- Coverage: Transaction, ChangeLine and MinimalChangeStrategy have 100% line and branch coverage.
- Status: **done**.

### T-004 — red
- Tests: `RandomChangeStrategyTests`: sums exactly over 1000 seeds × 0..500¢ (AC-005); ≥2 distinct results across 20 seeds for 167¢ (AC-004); same seed gives the same result; largest first with no zero counts (AC-007); zero → empty; negative throws.
- Stage 1: compile failure. Stage 2: a stub returning `[]`.
- Run → 3 failed / 36 passed. The ordering, same-seed and zero tests pass trivially on an empty list; the three core assertions fail, e.g. `distinctResults should be greater than 1 but was 1`.

### T-004 — green
- `RandomChangeStrategy(Random)`, per ADR-003: for each denomination largest first, take `NextInt64(0, mostThatFit + 1)`; the smallest denomination takes all that fits, so the total is exact.
- Unit suite 39/39 passed.

### T-004 — refactor
- Same shape as `MinimalChangeStrategy` (early `continue`, subtract `line.Total`). No structural change needed. Suite green.

### T-004 — simplify
- Named the local `mostThatFit`, and `smallest` for the remainder rule. The doc comment explains why the total is always exact.
- Coverage first showed an 87.5% branch rate (the null-`Random` guard was untested). Added `Constructor_NullRandom_Throws`, giving 100% line and branch coverage.
- `dotnet format --verify-no-changes` → 0; Release build → 0 warnings; full suite 41/41 passed (40 unit + 1 integration).
- Status: **done**.

### T-005 — red
- Tests: `InMemoryDivisorSettingsTests` (default 3; change; rejects < 1 and keeps the value, AC-025), `OwedDivisibleByRuleTests` (333/300 match and 212/197 don't with divisor 3, AC-004; divisor 5, AC-013; strategy is random), `ChangeCalculatorTests` (no match → minimal, AC-003; divisible → random, AC-004; lowest priority wins, and ties go to the first registered, AC-024; a divisor change applies to the next calculation, AC-025; an unregistered strategy or a missing minimal strategy throws).
- Stage 1: compile failure. Stage 2: stubs.
- CA1716 rejected `IDivisorSettings.Set` (a reserved keyword in VB), so it was renamed to `Change` in the interface, implementation and tests.
- Run → 16 failed / 42 passed. The two "not divisible" cases pass trivially because the stub always returns false.

### T-005 — green
- `IChangeRule { Priority, StrategyType, Matches }`, `IDivisorSettings { Current, Change }`, `InMemoryDivisorSettings` (default 3, rejects < 1, `Volatile` reads/writes), internal `OwedDivisibleByRule` (owed % divisor == 0 → `RandomChangeStrategy`), internal `ChangeCalculator` (stable `OrderBy(Priority)`, first match wins, otherwise `MinimalChangeStrategy`; checks at construction that the minimal strategy and every rule's strategy are registered).
- First green run: 57/58. `Calculate_DivisorChanged_AppliesToNextCalculation` had a test bug: minimal change for 100¢ is "1 dollar", whose count is also 1, so the count couldn't tell the strategies apart. The test now asserts the denomination (the marker) instead. 58/58.

### T-005 — refactor
- Named `OwedDivisibleByRule.DefaultPriority = 100`, and `InMemoryDivisorSettings.DefaultDivisor` / `MinimumDivisor`.
- The calculator resolves strategies through a single `StrategyOfType` helper, used both by the startup check and the default. Suite green.

### T-005 — simplify
- Coverage gaps closed with tests: the `OwedDivisibleByRule` null guard (branch 50% → 100%), and `Priority` (unread because `OrderBy` skips the key for a single element; line 87.5% → 100%).
- `dotnet format --verify-no-changes` → 0; Release build → 0 warnings; full suite 61/61 passed.
- Coverage: ChangeCalculator, InMemoryDivisorSettings and OwedDivisibleByRule have 100% line and branch coverage.
- Status: **done**.

### T-006 — red
- Tests: `ChangeFormatterTests`, with one test per AC: AC-006 (comma-separated), AC-007 (largest first from unordered input), AC-008 (zero counts omitted), AC-009 / AC-010 (singular/plural), AC-011 / AC-012 (README samples through `MinimalChangeStrategy`), AC-016 (`No change` for no lines and for only-zero lines).
- Stage 1: compile failure. Stage 2: a stub returning `""`.
- Run → 8 failed / 61 passed. `Format_OnlyZeroCountLines_IsNoChange` passes trivially because the stub constant is also `""`. Example: `should be "3 quarters,1 dime,3 pennies" but was ""`.

### T-006 — green
- `ChangeFormatter.Format(lines)`: drops zero counts, sorts by value descending, renders `<count> <NameFor(count)>`, joins with `,`; returns `NoChange = "No change"` when nothing is left.
- Unit suite 69/69 passed.

### T-006 — refactor
- Named the `EntrySeparator` constant. The formatter re-sorts defensively, so the output order does not depend on the strategy (AC-007). Suite green.

### T-006 — simplify
- A plain `if` for the `No change` branch instead of a ternary. Each LINQ step is annotated with its AC.
- `.editorconfig`: added a PascalCase rule for private `const` fields. The underscore rule had flagged `EntrySeparator` (IDE1006).
- `dotnet format --verify-no-changes` → 0; Release build → 0 warnings; full suite 70/70 passed.
- Coverage: ChangeFormatter has 100% line and branch coverage.
- Status: **done**.
