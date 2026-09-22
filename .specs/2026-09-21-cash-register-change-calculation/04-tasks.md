# Tasks: 2026-09-21-cash-register-change-calculation

> Owner: `dotnet-architect` · Phase 3 · Template: `.claude/templates/tasks.template.md`
>
> One task ≈ 1–4 hours. Run each with `/net-build <task-id>`. Paths are exact, and `enforce-files-in-scope.sh` enforces them.

## Inputs

- `03-design.md` revision: working tree 2026-09-21 (ADR-001 to ADR-008)

## Task Index

| ID | Title | AC-IDs | Depends on | Gates |
|---|---|---|---|---|
| T-001 | Scaffold solution, module, host and test projects | — (scaffold) | — | unit |
| T-002 | Currencies slice: Denomination, Currency, USD, registry | AC-009, AC-010, AC-029 | T-001 | unit, coverage |
| T-003 | Transaction + minimal change strategy | AC-003, AC-005 | T-002 | unit, coverage |
| T-004 | Random change strategy | AC-004, AC-005 | T-003 | unit, coverage |
| T-005 | Rules, divisor settings, ChangeCalculator | AC-003, AC-004, AC-013, AC-024, AC-025 | T-004 | unit, coverage |
| T-006 | Change formatter | AC-006, AC-007, AC-008, AC-009, AC-010, AC-011, AC-012, AC-016 | T-003 | unit, coverage |
| T-007 | Transaction line parser | AC-017, AC-018, AC-019, AC-020, AC-029 | T-003 | unit, coverage |
| T-008 | Change file processor + logging | AC-001, AC-002, AC-011, AC-012, AC-021, AC-022, AC-023 | T-005, T-006, T-007 | unit, coverage |
| T-009 | Module registration + API host (ProblemDetails, health, CORS) | AC-015 | T-008 | unit, it, coverage |
| T-010 | File upload / list / download endpoints | AC-001, AC-015, AC-023, AC-027, AC-028 | T-009 | unit, it, coverage |
| T-011 | Divisor settings endpoints | AC-015, AC-025, AC-026 | T-009 | unit, it, coverage |
| T-012 | Architecture rules + NFR-001 performance test | AC-001, AC-024 (DC-001–003, NFR-001) | T-010, T-011 | unit, it |
| T-013 | Web scaffold + typed API client | AC-015 | T-010, T-011 | web-lint, web-typecheck, web-unit |
| T-014 | Divisor settings UI | AC-025, AC-026 | T-013 | web-lint, web-typecheck, web-unit |
| T-015 | Upload, uploaded-files list and download UI | AC-023, AC-027, AC-028 | T-013 | web-lint, web-typecheck, web-unit, web-build |
| T-016 | Run instructions + design notes in README | — (docs) | T-015 | unit |
| T-017 | Unit-test the endpoint handlers (coverage gate, ADR-009) | AC-015, AC-023, AC-025, AC-026, AC-027, AC-028 | T-016 | unit, coverage |
| T-018 | Extract host composition from Program.cs (review F-001) | AC-015 | T-017 | unit, it, coverage |

## AC Coverage

| AC | Tasks |
|---|---|
| AC-001 | T-008, T-010, T-012 |
| AC-002 | T-008 |
| AC-003 | T-003, T-005 |
| AC-004 | T-004, T-005 |
| AC-005 | T-003, T-004 |
| AC-006 – AC-010 | T-006 (AC-009, AC-010 also T-002) |
| AC-011, AC-012 | T-006, T-008 |
| AC-013 | T-005 |
| AC-014 | withdrawn → DC-001, covered by T-012 |
| AC-015 | T-009, T-010, T-011, T-013 |
| AC-016 | T-006 |
| AC-017 – AC-020 | T-007 |
| AC-021, AC-022 | T-008 |
| AC-023 | T-008, T-010, T-015 |
| AC-024 | T-005, T-012 |
| AC-025 | T-005, T-011, T-014 |
| AC-026 | T-011, T-014 |
| AC-027, AC-028 | T-010, T-015 |
| AC-029 | T-002, T-007 |

All 28 active ACs are covered. ✅

## Tasks

### T-001: Scaffold solution, module, host and test projects
- **AC-IDs:** none (scaffold)
- **Test-IDs:** T-001-T1 (solution builds; the unit test host runs a passing smoke test), T-001-T2 (the integration test host runs a passing smoke test)
- **Files in scope:**
  - `CashRegister.sln` (deleted, replaced by `CashRegister.slnx`)
  - `CashRegister.slnx`
  - `global.json`
  - `Directory.Build.props`
  - `Directory.Packages.props`
  - `.editorconfig`
  - `.gitignore`
  - `src/CashRegister/CashRegister.csproj`
  - `src/CashRegister.Api/CashRegister.Api.csproj`
  - `src/CashRegister.Api/Program.cs`
  - `src/CashRegister.Api/appsettings.json`
  - `src/CashRegister.Api/appsettings.Development.json`
  - `src/CashRegister.Api/Properties/launchSettings.json`
  - `tests/CashRegister.Tests/CashRegister.Tests.csproj`
  - `tests/CashRegister.Tests/ScaffoldSmokeTests.cs`
  - `tests/CashRegister.IntegrationTests/CashRegister.IntegrationTests.csproj`
  - `tests/CashRegister.IntegrationTests/ScaffoldSmokeTests.cs`
  - Delete: `src/CashRegister.Domain/**`, `src/CashRegister.Application/**`, `tests/CashRegister.Domain.Tests/**`, `tests/CashRegister.Application.Tests/**`, `tests/CashRegister.Api.Tests/**` (first-pass scaffold, ADR-001)
- **Dependencies:** none
- **Gates:** unit
- **Rollback:** `git clean` the new folders and restore the previous files.
- **Notes:** Use xUnit v3 (`xunit.v3`), Microsoft.Testing.Platform (`UseMicrosoftTestingPlatformRunner`), Shouldly, NSubstitute, `Microsoft.Extensions.Diagnostics.Testing` (FakeLogger) and TngTech.ArchUnitNET.xUnitV3 through central package management. The module project adds `<FrameworkReference Include="Microsoft.AspNetCore.App" />` and `InternalsVisibleTo` for both test projects. `Program` ends with `public partial class Program;` for `WebApplicationFactory`.

### T-002: Currencies slice
- **AC-IDs:** AC-009, AC-010, AC-029
- **Test-IDs:** T-002-T1 (`NameFor(1)` returns singular, AC-009), T-002-T2 (`NameFor(2)` returns plural, AC-010), T-002-T3 (USD has dollar/quarter/dime/nickel/penny ordered by value descending), T-002-T4 (USD decimal separator `.` and 2 minor digits, AC-029), T-002-T5 (a currency without a 1-unit denomination, or with duplicate values, throws), T-002-T6 (registry resolves `usd` case-insensitively; unknown code returns false)
- **Files in scope:**
  - `src/CashRegister/Features/Currencies/Denomination.cs`
  - `src/CashRegister/Features/Currencies/Currency.cs`
  - `src/CashRegister/Features/Currencies/UsdCurrency.cs`
  - `src/CashRegister/Features/Currencies/ICurrencyRegistry.cs`
  - `src/CashRegister/Features/Currencies/CurrencyRegistry.cs`
  - `tests/CashRegister.Tests/Features/Currencies/DenominationTests.cs`
  - `tests/CashRegister.Tests/Features/Currencies/UsdCurrencyTests.cs`
  - `tests/CashRegister.Tests/Features/Currencies/CurrencyRegistryTests.cs`
- **Dependencies:** T-001
- **Gates:** unit, coverage
- **Rollback:** delete the listed files.

### T-003: Transaction + minimal change strategy
- **AC-IDs:** AC-003, AC-005
- **Test-IDs:** T-003-T1 (88¢ → 3 quarters, 1 dime, 3 pennies, AC-003), T-003-T2 (3¢ → 3 pennies), T-003-T3 (0 → empty), T-003-T4 (sum of lines equals change for 0..1000¢, AC-005), T-003-T5 (Transaction rejects negative amounts and paid < owed)
- **Files in scope:**
  - `src/CashRegister/Features/Change/Money/Transaction.cs`
  - `src/CashRegister/Features/Change/Money/ChangeLine.cs`
  - `src/CashRegister/Features/Change/Strategies/IChangeStrategy.cs`
  - `src/CashRegister/Features/Change/Strategies/MinimalChangeStrategy.cs`
  - `tests/CashRegister.Tests/Features/Change/Money/TransactionTests.cs`
  - `tests/CashRegister.Tests/Features/Change/Strategies/MinimalChangeStrategyTests.cs`
- **Dependencies:** T-002
- **Gates:** unit, coverage
- **Rollback:** delete the listed files.

### T-004: Random change strategy
- **AC-IDs:** AC-004, AC-005
- **Test-IDs:** T-004-T1 (the sum equals the change for 1000 seeds × amounts 0..500¢, AC-005), T-004-T2 (the same seed gives the same result), T-004-T3 (different seeds produce at least two distinct results for 167¢, AC-004), T-004-T4 (lines ordered by value descending, no zero counts)
- **Files in scope:**
  - `src/CashRegister/Features/Change/Strategies/RandomChangeStrategy.cs`
  - `tests/CashRegister.Tests/Features/Change/Strategies/RandomChangeStrategyTests.cs`
- **Dependencies:** T-003
- **Gates:** unit, coverage
- **Rollback:** delete the listed files.

### T-005: Rules, divisor settings, ChangeCalculator
- **AC-IDs:** AC-003, AC-004, AC-013, AC-024, AC-025
- **Test-IDs:** T-005-T1 (owed 333¢ with divisor 3 → random strategy, AC-004), T-005-T2 (owed 212¢ with divisor 3 → minimal, AC-003), T-005-T3 (divisor set to 5: 500¢ random and 333¢ minimal, AC-013), T-005-T4 (two matching rules → lowest priority wins, AC-024), T-005-T5 (changing the settings affects the next calculation, AC-025), T-005-T6 (settings default to 3 and reject values < 1), T-005-T7 (a rule referencing an unregistered strategy fails at construction)
- **Files in scope:**
  - `src/CashRegister/Features/Change/Rules/IChangeRule.cs`
  - `src/CashRegister/Features/Change/Rules/OwedDivisibleByRule.cs`
  - `src/CashRegister/Features/Change/Rules/IDivisorSettings.cs`
  - `src/CashRegister/Features/Change/Rules/InMemoryDivisorSettings.cs`
  - `src/CashRegister/Features/Change/ChangeCalculator.cs`
  - `tests/CashRegister.Tests/Features/Change/Rules/OwedDivisibleByRuleTests.cs`
  - `tests/CashRegister.Tests/Features/Change/Rules/InMemoryDivisorSettingsTests.cs`
  - `tests/CashRegister.Tests/Features/Change/ChangeCalculatorTests.cs`
- **Dependencies:** T-004
- **Gates:** unit, coverage
- **Rollback:** delete the listed files.

### T-006: Change formatter
- **AC-IDs:** AC-006, AC-007, AC-008, AC-009, AC-010, AC-011, AC-012, AC-016
- **Test-IDs:** one test per AC, each tagged `[Trait("AC", …)]`. They include `3 quarters,1 dime,3 pennies` (AC-011), `3 pennies` (AC-012), `No change` for empty lines (AC-016), `1 dollar` singular, and ordering when the input is unsorted (AC-007).
- **Files in scope:**
  - `src/CashRegister/Features/Change/Formatting/ChangeFormatter.cs`
  - `tests/CashRegister.Tests/Features/Change/Formatting/ChangeFormatterTests.cs`
- **Dependencies:** T-003
- **Gates:** unit, coverage
- **Rollback:** delete the listed files.

### T-007: Transaction line parser
- **AC-IDs:** AC-017, AC-018, AC-019, AC-020, AC-029
- **Test-IDs:** T-007-T1 (`2.12,3.00` → 212/300), T-007-T2 (`abc`, `1.00`, `1,2,3`, `1.00;2.00` give the invalid-line error, AC-017), T-007-T3 (`-1.00,2.00` → negative error, AC-018), T-007-T4 (`3.00,2.00` → paid-less error, AC-019), T-007-T5 (`1.001,2.00` → decimals error, AC-020), T-007-T6 (whitespace around fields is trimmed; `5,6` parses as whole dollars), T-007-T7 (uses the currency's decimal separator: a test currency with `'` parses `1'50`, AC-029), T-007-T8 (`1,000.00`-style thousands separators are rejected as invalid)
- **Files in scope:**
  - `src/CashRegister/Features/Change/Parsing/TransactionLineParser.cs`
  - `src/CashRegister/Features/Change/Parsing/LineParseResult.cs`
  - `src/CashRegister/Features/Change/Parsing/LineErrors.cs`
  - `tests/CashRegister.Tests/Features/Change/Parsing/TransactionLineParserTests.cs`
- **Dependencies:** T-003
- **Gates:** unit, coverage
- **Rollback:** delete the listed files.

### T-008: Change file processor + logging
- **AC-IDs:** AC-001, AC-002, AC-011, AC-012, AC-021, AC-022, AC-023
- **Test-IDs:** T-008-T1 (README sample with blank lines gives 3 output lines, AC-001, AC-022), T-008-T2 (order preserved, AC-002), T-008-T3 (lines 1–2 are exactly the README output, AC-011, AC-012), T-008-T4 (an invalid middle line gives an error on that line and the following lines are still processed, AC-021), T-008-T5 (1000 non-blank lines OK; 1001 → `LineLimitExceeded`, AC-023), T-008-T6 (an empty file gives zero lines), T-008-T7 (`FileProcessed` log event emitted with counts, NFR-002, via `FakeLogger`)
- **Files in scope:**
  - `src/CashRegister/Features/Change/Processing/ChangeFileProcessor.cs`
  - `src/CashRegister/Features/Change/Processing/ProcessedFile.cs`
  - `src/CashRegister/Features/Change/Processing/CashRegisterLog.cs`
  - `tests/CashRegister.Tests/Features/Change/Processing/ChangeFileProcessorTests.cs`
- **Dependencies:** T-005, T-006, T-007
- **Gates:** unit, coverage
- **Rollback:** delete the listed files.

### T-009: Module registration + API host
- **AC-IDs:** AC-015
- **Test-IDs:** T-009-T1 (`AddCashRegister` resolves `ChangeFileProcessor`, the calculator and both strategies; an unknown `CashRegister:Currency` fails at startup), T-009-T2 (IT: `/health` returns 200, NFR-003), T-009-T3 (IT: an unknown route under `/api` returns a 404 ProblemDetails body, AC-015), T-009-T4 (IT: CORS preflight from `http://localhost:5173` is allowed and from another origin is not)
- **Files in scope:**
  - `src/CashRegister/CashRegisterModule.cs`
  - `src/CashRegister/CashRegisterOptions.cs`
  - `src/CashRegister.Api/Program.cs`
  - `src/CashRegister.Api/appsettings.json`
  - `src/CashRegister.Api/appsettings.Development.json`
  - `tests/CashRegister.Tests/CashRegisterModuleTests.cs`
  - `tests/CashRegister.IntegrationTests/CashRegisterApiFactory.cs`
  - `tests/CashRegister.IntegrationTests/HostTests.cs`
- **Dependencies:** T-008
- **Gates:** unit, it, coverage
- **Rollback:** revert `Program.cs` and delete the new files.

### T-010: File upload / list / download endpoints
- **AC-IDs:** AC-001, AC-015, AC-023, AC-027, AC-028
- **Test-IDs:** T-010-T1 (store: add/get/list newest first, thread-safe), T-010-T2 (IT: POST the README sample → 201, Location, lineCount 3, AC-001), T-010-T3 (IT: GET `/api/files` lists the upload, AC-027), T-010-T4 (IT: GET `/api/files/{id}/output` → text/plain attachment whose first two lines match the README, AC-028), T-010-T5 (IT: 1001 lines → 400 ProblemDetails `Too many lines`, AC-023), T-010-T6 (IT: missing file → 400 ProblemDetails, AC-015), T-010-T7 (IT: unknown id → 404 ProblemDetails, AC-015), T-010-T8 (file name sanitised in `Content-Disposition`)
- **Files in scope:**
  - `src/CashRegister/Features/Change/Files/IUploadedFileStore.cs`
  - `src/CashRegister/Features/Change/Files/InMemoryUploadedFileStore.cs`
  - `src/CashRegister/Features/Change/Files/UploadedFile.cs`
  - `src/CashRegister/Features/Change/Files/FileDtos.cs`
  - `src/CashRegister/Features/Change/Files/FileEndpoints.cs`
  - `src/CashRegister/CashRegisterModule.cs`
  - `tests/CashRegister.Tests/Features/Change/Files/InMemoryUploadedFileStoreTests.cs`
  - `tests/CashRegister.IntegrationTests/Features/Change/Files/FileEndpointsTests.cs`
- **Dependencies:** T-009
- **Gates:** unit, it, coverage
- **Rollback:** remove the `MapFileEndpoints` call and delete the listed files.

### T-011: Divisor settings endpoints
- **AC-IDs:** AC-015, AC-025, AC-026
- **Test-IDs:** T-011-T1 (IT: GET → 3 by default), T-011-T2 (IT: PUT 5 → 200, then an upload with owed 5.00 is processed as random: the sum is correct and the settings are used, AC-025), T-011-T3 (IT: PUT 0 and PUT -2 → 400 ProblemDetails, AC-026), T-011-T4 (IT: PUT `"abc"` / 2.5 → 400 ProblemDetails, AC-015), T-011-T5 (`DivisorChanged` log event)
- **Files in scope:**
  - `src/CashRegister/Features/Change/Settings/DivisorDtos.cs`
  - `src/CashRegister/Features/Change/Settings/DivisorEndpoints.cs`
  - `src/CashRegister/Features/Change/Processing/CashRegisterLog.cs` (adds the `DivisorChanged` event; moved here from T-008)
  - `src/CashRegister/CashRegisterModule.cs`
  - `src/CashRegister.Api/Program.cs` (added during T-011: map `BadHttpRequestException` to its own 400 status instead of 500)
  - `tests/CashRegister.IntegrationTests/Features/Change/Settings/DivisorEndpointsTests.cs`
- **Dependencies:** T-009 (serialize with T-010 on `CashRegisterModule.cs`: run T-010 first)
- **Gates:** unit, it, coverage
- **Rollback:** remove the `MapDivisorEndpoints` call and delete the listed files.

### T-012: Architecture rules + NFR-001 performance test
- **AC-IDs:** AC-001, AC-024 (cross-cutting: DC-001, DC-002, DC-003, NFR-001)
- **Test-IDs:** T-012-T1 to T-012-T5 (the five ArchUnitNET rules in `03-design.md`), T-012-T6 (IT `[Trait("Category","Performance")]`: 50 uploads of a 1000-line file, p95 < 500 ms)
- **Files in scope:**
  - `tests/CashRegister.Tests/Architecture/ArchitectureTests.cs`
  - `tests/CashRegister.IntegrationTests/Performance/FileUploadPerformanceTests.cs`
- **Dependencies:** T-010, T-011
- **Gates:** unit, it
- **Rollback:** delete the listed files.

### T-013: Web scaffold + typed API client
- **AC-IDs:** AC-015 (ProblemDetails surfaced as `ApiError`)
- **Test-IDs:** T-013-T1 (client parses a ProblemDetails 400 into `ApiError` with title/detail), T-013-T2 (`listFiles` maps JSON to typed summaries), T-013-T3 (`uploadFile` posts multipart with field `file`)
- **Files in scope:**
  - `web/package.json`
  - `web/package-lock.json`
  - `web/tsconfig.json`
  - `web/tsconfig.node.json`
  - `web/vite.config.ts`
  - `web/eslint.config.js`
  - `web/index.html`
  - `web/src/main.tsx`
  - `web/src/App.tsx`
  - `web/src/index.css`
  - `web/src/api/client.ts`
  - `web/src/api/types.ts`
  - `web/src/test/setup.ts`
  - `web/src/test/server.ts`
  - `web/src/api/client.test.ts`
- **Dependencies:** T-010, T-011
- **Gates:** web-lint, web-typecheck, web-unit
- **Rollback:** delete `web/`.

### T-014: Divisor settings UI
- **AC-IDs:** AC-025, AC-026
- **Test-IDs:** T-014-T1 (loads and shows the current divisor), T-014-T2 (saving 5 calls PUT and shows a confirmation, AC-025), T-014-T3 (a server 400 shows the ProblemDetails message, AC-026), T-014-T4 (value 1 shows the "every transaction will be random" warning), T-014-T5 (the input is labelled and accessible)
- **Files in scope:**
  - `web/src/features/divisor/DivisorSettings.tsx`
  - `web/src/features/divisor/DivisorSettings.test.tsx`
  - `web/src/App.tsx`
- **Dependencies:** T-013
- **Gates:** web-lint, web-typecheck, web-unit
- **Rollback:** delete the listed files and remove the component from `App.tsx`.

### T-015: Upload, uploaded-files list and download UI
- **AC-IDs:** AC-023, AC-027, AC-028
- **Test-IDs:** T-015-T1 (a successful upload adds the entry to the list, AC-027), T-015-T2 (each entry has a Download link to `/api/files/{id}/output`, AC-028), T-015-T3 (a 400 `Too many lines` error is shown, AC-023), T-015-T4 (the list loads on mount, newest first), T-015-T5 (the upload button is disabled until a file is chosen)
- **Files in scope:**
  - `web/src/features/files/FileUpload.tsx`
  - `web/src/features/files/FileUpload.test.tsx`
  - `web/src/features/files/UploadedFilesList.tsx`
  - `web/src/features/files/UploadedFilesList.test.tsx`
  - `web/src/App.tsx`
  - `web/src/index.css` (added during T-015: table styles and horizontal scroll on narrow screens)
- **Dependencies:** T-013 (serialize with T-014 on `App.tsx`: run T-014 first)
- **Gates:** web-lint, web-typecheck, web-unit, web-build
- **Rollback:** delete the listed files and remove the components from `App.tsx`.

### T-016: Run instructions + design notes in README
- **AC-IDs:** none (docs)
- **Test-IDs:** T-016-T1 (the full suite still passes)
- **Files in scope:**
  - `README.md`
  - `samples/input.txt`
- **Dependencies:** T-015
- **Gates:** unit
- **Rollback:** revert `README.md`.
- **Notes:** Append a "Solution" section covering how to run the API, web and tests, and answer the three "Things to Consider" by pointing to ADR-003 and ADR-004. Do not edit the original problem text.

### T-017: Unit-test the endpoint handlers
- **AC-IDs:** AC-015, AC-023, AC-025, AC-026, AC-027, AC-028
- **Why:** the harness measures coverage on the unit run only (ADR-009). The endpoints are proved only by integration tests, so unit coverage is 73.2% line / 75.4% branch, under the 90% floor. This task adds unit tests for the handler logic; thresholds are not lowered.
- **Test-IDs:** T-017-T1 (upload with no file → `Invalid file` problem, AC-015), T-017-T2 (1001 lines → `Too many lines` problem, AC-023), T-017-T3 (upload stores the file with `FakeTimeProvider` time and returns 201 + summary, AC-027), T-017-T4 (list maps newest-first summaries, AC-027), T-017-T5 (download returns a text/plain file named `<name>-change.txt`, AC-028), T-017-T6 (download unknown id → 404 problem, AC-015), T-017-T7 (file name sanitising: path stripped, fallback, 100-character cap), T-017-T8 (divisor GET returns the current value), T-017-T9 (divisor PUT valid → 200 + `DivisorChanged` log, AC-025), T-017-T10 (divisor PUT missing or < 1 → `Invalid divisor`, value unchanged, AC-026)
- **Files in scope:**
  - `src/CashRegister/Features/Change/Files/FileEndpoints.cs` (handlers `private` → `internal` so unit tests can call them; no behaviour change)
  - `src/CashRegister/Features/Change/Settings/DivisorEndpoints.cs` (same)
  - `tests/CashRegister.Tests/Features/Change/Files/FileEndpointsTests.cs`
  - `tests/CashRegister.Tests/Features/Change/Settings/DivisorEndpointsTests.cs`
  - `tests/CashRegister.Tests/HostCompositionTests.cs` (added during T-017, user decision: a host test in the unit gate covers `Program.cs`)
  - `tests/CashRegister.Tests/CashRegister.Tests.csproj` (references the API host + `Microsoft.AspNetCore.Mvc.Testing`, already centrally versioned)
- **Dependencies:** T-016
- **Gates:** unit, coverage (the harness coverage gate must pass: ≥ 90% line and branch on the unit run)
- **Rollback:** delete the two test files and restore `private` on the handlers.
- **Notes:** Call the handlers directly with real `ChangeFileProcessor`/`InMemoryUploadedFileStore`/`InMemoryDivisorSettings` and a fake `IFormFile` (`FormFile` over a `MemoryStream`); assert on the `TypedResults` values. `Microsoft.Extensions.TimeProvider.Testing` (`FakeTimeProvider`) is a new test package and needs approval; otherwise use a small hand-written `TimeProvider` subclass in the test.

### T-018: Extract host composition from Program.cs
- **AC-IDs:** AC-015
- **Why:** code review `08-code-review.md` F-001 (major): `Program.cs` holds branching and expression logic (the Development/Production switches, the status-code selector, the CORS origins fallback) that can only be tested by booting the host.
- **Test-IDs:** T-018-T1 (the status-code selector keeps a `BadHttpRequestException`'s own status: 400, 413), T-018-T2 (other exceptions → 500), T-018-T3 (CORS origins read from config; a missing section → none), T-018-T4 (JSON console logging outside Development only), T-018-T5 (OpenAPI mapped in Development only)
- **Files in scope:**
  - `src/CashRegister.Api/HostSetup.cs` (new: `AddCashRegisterHost` / `UseCashRegisterHost` extensions plus two small testable helpers)
  - `src/CashRegister.Api/Program.cs` (reduced to build → use → map → run; no branches)
  - `tests/CashRegister.Tests/HostSetupTests.cs`
  - `src/CashRegister.Api/CashRegister.Api.csproj` (added during T-018: `InternalsVisibleTo` for `CashRegister.Tests`)
- **Dependencies:** T-017
- **Gates:** unit, it, coverage (all existing host/integration tests must stay green, since behaviour is unchanged)
- **Rollback:** restore `Program.cs` and delete `HostSetup.cs` and its tests.

### X-001: Files produced outside `/net-build` tasks (registered for the `/net-ship` scope check)

These files were created by workflow phases or user requests that are not TDD tasks. Each was reviewed in `08-code-review.md` (`9b945d3...HEAD`) and is covered by the harness run in `07-validation-report.md` revision 2.

| Produced by | Date | Files |
|---|---|---|
| `/net-test` (cross-cutting suites, Gap-007) | 2026-09-21 | `tests/CashRegister.IntegrationTests/OpenApiDocumentTests.cs`, `tests/CashRegister.IntegrationTests/ProductionHostTests.cs`, `web/src/App.test.tsx` |
| `dotnet-build-harness` wiring (ADR-009) | 2026-09-21 | `.github/scripts/harness-dotnet.sh`, `.github/scripts/check-new-code-coverage-dotnet.sh`, `.github/scripts/traceability-dotnet.sh`, `.github/scripts/lib/harness.mjs`, `.github/scripts/lib/cobertura.mjs`, `.github/scripts/lib/new-code-coverage.mjs`, `.github/scripts/lib/traceability.mjs`, `tests/Directory.Build.props`, `artifacts/openapi/openapi.json` (generated contract, committed on purpose) |
| User request: run API + UI together with Docker | 2026-09-21 | `docker-compose.yml`, `.dockerignore`, `src/CashRegister.Api/Dockerfile`, `web/Dockerfile`, `web/.dockerignore`, `web/nginx.conf` |

## Cross-cutting items (Phase 5)

- ArchUnitNET rules (T-012)
- OpenAPI document smoke test (`/openapi/v1.json` returns 200 in Development)
- A property-style test for the random strategy already lives in T-004

## Open Questions

- (none)

## Resolved Questions

- (none)

## Sign-off

- [x] Every AC from `01-spec.md` is covered by at least one task.
- [x] Every task has Test-IDs and Files-in-scope.
- [x] All `Q-NNN` resolved or deferred-with-rationale.
- [x] Reviewed by user on 2026-09-21.
