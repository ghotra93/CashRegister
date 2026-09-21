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

### T-007 — red
- Tests: `TransactionLineParserTests`, 24 cases: valid → cents; accepted shapes (whitespace, whole dollars, one decimal, zero); AC-017 malformed (11 shapes including thousands separators, `$`, `1.`, `.50`, overflow); AC-018 negative; AC-019 paid < owed; AC-020 too many decimals; check order (negative before decimals); AC-029 separator from the currency (an apostrophe currency parses `1'50`; USD rejects it).
- Stage 1: compile failure. Stage 2: stubs.
- Run → 24 failed / 69 passed.

### T-007 — green
- `LineErrors` (ADR-007 wording; the decimals message takes its digit count from the currency). `LineParseResult` (private constructor, `Success` / `Failure` factories). `TransactionLineParser.Parse(line, currency)`: split on `,`, then parse each field with `ParsedAmount.From` (trim, optional leading `-`, digits, optional currency decimal separator + digits). Checks run in order: format → negative → decimals → overflow (treated as invalid) → paid < owed.
- Unit suite 93/93 passed.

### T-007 — refactor
- Moved the per-amount parsing into a private nested `ParsedAmount` record, so `Parse` reads as the ADR-007 check sequence. Suite green.

### T-007 — simplify
- `ParsedAmount.From`: replaced the repeated `parts.Length == 2` checks and the combined boolean with sequential guard clauses. `ToMinorUnits`: an `if` instead of the `TryParse` ternary.
- Coverage showed `ParsedAmount` at 94% line / 93.75% branch, under the 95% target for new code. Added malformed cases `1.2.3,4.00` (two separators) and `1.a0,2.00` (non-digit fraction), giving 100% / 100%.
- `dotnet format --verify-no-changes` → 0; Release build → 0 warnings; full suite 96/96 passed.
- Status: **done**.

### T-008 — red
- Tests: `ChangeFileProcessorTests`, 13 cases: README sample → 3 lines (AC-001); blank/whitespace lines ignored (AC-022); order kept (AC-002); lines 1 and 2 equal the README output (AC-011, AC-012); an invalid middle line gets an error and processing continues (AC-021); `No change`; exactly 1000 lines OK and 1001 rejected (AC-023); empty file; LF join with no trailing newline (ADR-007); `FileProcessed` log with counts (NFR-002) and a `FileRejected` warning, checked with `FakeLogger`.
- Stage 1: compile failure. Stage 2: stubs.
- Run → 12 failed / 96 passed. The empty-file case passes trivially against the stub.

### T-008 — green
- `ProcessedFile` (private constructor; `Completed` / `TooManyLines` factories; `LineCount`, `OutputText` joined with LF). `CashRegisterLog` (LoggerMessage source-generated `FileProcessed` Information and `FileRejected` Warning). `ChangeFileProcessor.ProcessAsync(stream, fileName, currency, ct)`: reads line by line, skips blank lines, returns `TooManyLines` and stops reading at the 1001st non-blank line, otherwise parse → calculate → format, or writes the line error and continues. Logs `FileProcessed` with counts and elapsed ms.
- Unit suite 108/108 passed.

### T-008 — refactor
- Replaced `parsed.Transaction is null` + `parsed.Error!` with a pattern match on `Transaction`. Suite green.

### T-008 — simplify
- Removed a dead `?? LineErrors.InvalidLine` fallback, since `LineParseResult` guarantees an error when there is no transaction; a comment states that guarantee.
- Removed the unused `DivisorChanged` log event (YAGNI). **Plan change:** `CashRegisterLog.cs` was added to T-011's `files_in_scope` in `04-tasks.md` and `.tdd-state.json`, so T-011 can add the event when it is needed.
- Added `Constructor_NullDependencies_Throw` to cover the guards.
- **Design deviation:** `FileProcessed` logs the file name, not a file id. The id is assigned later by the file store (T-010), which can log it if needed.
- Coverage: ChangeFileProcessor, ProcessedFile and CashRegisterLog have 100% line and branch coverage.
- `dotnet format --verify-no-changes` → 0; Release build → 0 warnings; full suite 110/110 passed.
- Status: **done**.

### T-009 — red
- Unit: `CashRegisterModuleTests`: resolves `ChangeFileProcessor`; both strategies registered; one `OwedDivisibleByRule`; singleton `IDivisorSettings`; `ActiveCurrency` defaults to USD and is case-insensitive; an unknown currency fails options validation.
- Integration: `CashRegisterApiFactory` (Development environment) + `HostTests`: `/health` → 200 "Healthy" (NFR-003); unknown `/api` route → 404 `application/problem+json` (AC-015); CORS preflight from `http://localhost:5173` is allowed and from another origin is not.
- Stage 1: compile failure. Stage 2: stub `AddCashRegister` / `MapCashRegister` / options.
- Run → 9 failed / 111 passed. "Other origin not allowed" passes trivially while no CORS policy exists.
- `ActiveCurrency` is placed in `CashRegisterOptions.cs` (already in scope) because it is resolved from those options.

### T-009 — green
- `CashRegisterOptions` (section `CashRegister`, `Currency` default `USD`), `ActiveCurrency` record, `CashRegisterModule.AddCashRegister` (USD `Currency`, `CurrencyRegistry`, options + `ValidateOnStart`, `ActiveCurrency`, both strategies with `Random.Shared`, singleton `InMemoryDivisorSettings`, `OwedDivisibleByRule`, `ChangeCalculator`, `ChangeFileProcessor`), and `MapCashRegister` (empty; endpoints arrive in T-010 / T-011).
- `Program.cs`: ProblemDetails, exception handler, status-code pages, health checks at `/health`, OpenAPI (Development only), a named CORS policy from `Cors:AllowedOrigins` (Development: `http://localhost:5173`), and a JSON console logger outside Development.
- First run 119/120: the static `Validate(...)` message could not name the bad code. Replaced it with an `IValidateOptions<CashRegisterOptions>` validator (`CashRegisterOptionsValidator`) whose message names the code and lists the registered currencies. 120/120.

### T-009 — refactor
- Commented why `ActiveCurrency`'s factory can rely on the registry lookup (reading options `.Value` runs the validator first). Suite green.

### T-009 — simplify
- No further simplification. `dotnet format --verify-no-changes` → 0; Release build → 0 warnings; full suite 120/120 passed (unit 115 + integration 5).
- Coverage: unit tests give 98.8% line / 100% branch for the module package. `CashRegisterModule` is at 82.6% in the unit run because `MapCashRegister` runs only in the integration host; the integration run shows it at 100%. Both projects write `artifacts/coverage/cov.xml`, so the last run overwrites the other. **Follow-up for /net-validate:** give each project its own output name or merge the reports.
- Status: **done**.

### T-010 — red
- Unit: `InMemoryUploadedFileStoreTests`: add/get (AC-028), unknown id, newest first (AC-027), empty list, 200 concurrent adds.
- Integration: `FileEndpointsTests`: README upload → 201 + Location + lineCount 3 (AC-001); list contains uploads newest first (AC-027); download → text/plain attachment `readme-change.txt` whose first two lines match the README (AC-028); 1001 lines → 400 `Too many lines` (AC-023); no file → 400 `Invalid file` (AC-015); unknown id → 404 `File not found` (AC-015); path characters stripped from the file name; name truncated to 100 characters.
- Stage 1: compile failure. Stage 2: stubs.
- Run → 12 failed / 122 passed. The unknown-id and empty-list store tests pass trivially against the stub.

### T-010 — green
- `UploadedFile` record; `IUploadedFileStore` / `InMemoryUploadedFileStore` (`ConcurrentDictionary`, newest first by `UploadedAt`); `UploadedFileSummary` DTO.
- `FileEndpoints` (group `/api/files`):
  - `POST /` takes an `IFormFile` field `file`, has a 1 MB multipart limit, and disables antiforgery (ADR-005: no auth or cookies in v1). No file → 400 `Invalid file`; over the line limit → 400 `Too many lines`; otherwise the file is stored with `TimeProvider.GetUtcNow()` and the endpoint returns 201 + Location.
  - `GET /` lists files newest first.
  - `GET /{id:guid}/output` returns a `text/plain; charset=utf-8` attachment `<name>-change.txt`, or 404 `File not found`.
  - `SafeFileName` strips the path and invalid characters, falls back to `upload.txt`, and truncates to 100 characters.
- Module: registers the store and `TimeProvider.System` (`TryAdd`, so tests can swap it); `MapCashRegister` maps the file endpoints.
- Full suite 134/134 on the first green run.

### T-010 — refactor
- Every endpoint returns a typed `Results<…>` union, and the problem titles are stable strings. Suite green.

### T-010 — simplify
- Hoisted `Path.GetInvalidFileNameChars()` to a static field (it was allocated per character). Replaced the truncation ternary with an `if`.
- Coverage (the two projects now write separate files, `unit.xml` / `it.xml`): the store has 100% line and branch coverage from unit tests; `FileEndpoints` was at 93.75% in the integration run (the fallback-name branch was untested). Added `Upload_NameWithNothingLeftAfterStrippingThePath_FallsBackToUploadTxt` (name `folder/`; `HttpClient` refuses whitespace-only names), giving 100% / 100%.
- `dotnet format --verify-no-changes` → 0; Release build → 0 warnings; full suite 135/135 passed.
- Status: **done**.

### T-011 — red
- Integration: `DivisorEndpointsTests` (a fresh host per test, because the divisor is process-wide): GET → 3; PUT 5 → 200 and GET reflects it (AC-025); after PUT 5, `3.33,5.00` gives the exact minimal output `1 dollar,2 quarters,1 dime,1 nickel,2 pennies` (AC-025, deterministic); PUT 0 / -2 / `{}` → 400 `Invalid divisor` and the old value is kept (AC-026); PUT `"abc"` / `2.5` / `not json` → 400 ProblemDetails (AC-015); a `DivisorChanged` log with old/new values (via `AddFakeLogging`).
- The integration csproj now references `Microsoft.Extensions.Diagnostics.Testing` (already centrally versioned for the unit tests, so no new package).
- No stubs needed: the tests use HTTP only, so they compile and fail on assertions. Run → 10 failed / 15 passed.

### T-011 — green
- `DivisorResponse(int)`, `ChangeDivisorRequest(int?)`. The request value is nullable so `{}` is reported as invalid rather than read as 0.
- `DivisorEndpoints` (group `/api/settings/divisor`): `GET` → current value; `PUT` → rejects missing or < 1 with 400 `Invalid divisor` (the old value is kept), otherwise `IDivisorSettings.Change` + a `DivisorChanged` log (old → new) and 200.
- `CashRegisterLog.DivisorChanged` added (EventId 3), as re-planned in T-008. The module maps the divisor endpoints.
- First run: 6 failures.
  - (a) **Test bug**: the below-one cases read the response body twice (`ObjectDisposedException`). The helper now returns the parsed `ProblemDetails`.
  - (b) **Real bug**: non-integer JSON returned **500**. In Development, Minimal APIs throw `BadHttpRequestException` and `UseExceptionHandler` mapped it to 500. Fix in `Program.cs`: `ExceptionHandlerOptions.StatusCodeSelector` returns the exception's own status for `BadHttpRequestException` and 500 otherwise.
- **Plan change:** `src/CashRegister.Api/Program.cs` was added to T-011's `files_in_scope` in `04-tasks.md` and `.tdd-state.json` before the fix was made.
- Full suite 145/145.

### T-011 — refactor
- No structural changes needed. The divisor rules reuse `InMemoryDivisorSettings.MinimumDivisor` for both the check and the message. Suite green.

### T-011 — simplify
- `dotnet format` fixed the line endings in the new `Program.cs` lines. `dotnet format --verify-no-changes` → 0; Release build → 0 warnings; full suite 145/145 passed.
- Coverage (integration run): DivisorEndpoints, the DTOs and CashRegisterLog have 100% line and branch coverage. The two uncovered `Program` branches run only outside Development (JSON console logging, and the 500 fallback for non-request exceptions); the Development test host cannot reach them.
- Status: **done**.

### T-012 — red
- **Test-only task:** it verifies properties the existing code should already have, so correct code passes immediately. To keep the rules from passing vacuously, each "must not depend" rule is paired with a **positive control** proving ArchUnitNET can see the forbidden kind of dependency.
- `ArchitectureTests` (7 tests): Currencies must not depend on Change (+ control); the module does not reference `CashRegister.Api`; `IChangeRule` implementations are sealed and live in `Features.Change.Rules` (AC-024, DC-001); no `DateTime`/`DateTimeOffset` `.Now`/`.UtcNow` calls (TimeProvider only); only `*Endpoints` types in `Features` use `Microsoft.AspNetCore.Http` (+ control).
- First run → 2 failures, both positive controls:
  - `Endpoints_UseAspNetCoreHttp_PositiveControl`: only the module assembly was loaded, so no `Microsoft.AspNetCore.Http` types existed in the architecture. **The negative rule was vacuous.**
  - `Change_DependsOnCurrencies_PositiveControl`: `DependOnAny` applies to *every* matched type; the control was mis-specified.

### T-012 — green
- Loaded the ASP.NET Core HTTP assemblies (`HttpContext`, `IFormFile`, `TypedResults`, `StatusCodes`) into the ArchUnitNET architecture, so the Http rule is now real (its control passes).
- The Change → Currencies control now targets `Transaction` specifically.
- `FileUploadPerformanceTests` (NFR-001): 5 warm-up + 50 measured uploads of a mixed 1000-line file (minimal, random and error lines) through the real endpoint; asserts p95 < 500 ms. It passed, with all 55 uploads taking about 0.7 s in total.
- Unit 127/127; integration 26/26.

### T-012 — refactor
- A `TypesIn(namespace)` helper builds the anchored namespace regex once for all rules. Suite green.

### T-012 — simplify
- Replaced the second `[Trait("Category","Performance")]` with `[Trait("NFR","NFR-001")]`: xUnit v3 / MTP filtering did not match a second value for the same key (`--filter-trait Category=Performance` found 0 tests). `--filter-trait NFR=NFR-001` now selects exactly the performance test, and the test still runs under the `it` gate (`Category=Integration`, 26 tests).
- `dotnet format --verify-no-changes` → 0; Release build → 0 warnings; full suite 153/153 passed.
- Status: **done**.

### T-013 — red
- Scaffold: `web/` Vite 8 + React 19.3 + TypeScript **6.0** (pinned `~6.0.3`: typescript-eslint 8.70 peers `typescript <6.1`, so TS 7 is not yet usable), Vitest 5 (jsdom, globals), RTL 16, MSW 2, ESLint 10 flat config (`typescript-eslint` strictTypeChecked + react-hooks). Strict tsconfig (`noUncheckedIndexedAccess`, `exactOptionalPropertyTypes`). The Vite dev proxy sends `/api` and `/health` to `http://localhost:5080`.
- `npm install`: 0 vulnerabilities. MSW's postinstall (browser worker setup) is held back by npm allow-scripts; it is not needed for Node tests.
- Tests: `src/api/client.test.ts` (7): ProblemDetails → `ApiError` with title/detail (AC-015); a non-ProblemDetails error falls back to `Request failed (<status>)` (AC-015); `listFiles` is typed; `uploadFile` sends multipart field `file`; `outputUrl`; `getDivisor`; `setDivisor` PUTs JSON. AC ids are in the test names (`[AC-015]`), the TypeScript equivalent of `[Trait("AC", …)]`.
- Stub `client.ts` → `npx vitest run` → 7 failed. Example: `expected Error: not implemented to be an instance of ApiError`.

### T-013 — green
- `client.ts`: `ApiError(status, title, detail)`; a private `request<T>()` that throws `ApiError` from a ProblemDetails body, falling back to `Request failed (<status>)` for non-JSON errors; `listFiles`, `uploadFile` (multipart field `file`), `outputUrl` (id URL-encoded), `getDivisor`, `setDivisor` (PUT JSON).
- First run: 5/7. The two multipart tests failed with `Cannot read properties of undefined (reading '_buffer')`. A probe showed that Vitest's **jsdom** environment replaces `FormData` and `File` with jsdom's versions while `fetch` stays Node's (undici), which cannot serialize them. Restoring Node's globals would break `userEvent.upload` in T-015 (it needs the DOM's own `File`).
- **Deviation:** switched the test environment to **happy-dom** (`^20.14.5`, dev only), which ships a consistent fetch/FormData/File stack that MSW's Node interceptors still catch. `environmentOptions.happyDOM.url = http://localhost:5173` makes relative `/api/...` URLs resolve. jsdom was uninstalled. 7/7.
- Added `@types/node@24` (dev) for `tsconfig.node.json` (vite.config.ts), and `vite/client` types for the CSS import.

### T-013 — refactor
- Path constants `filesPath` / `divisorPath`; one `request<T>` helper for every call. Tests green.

### T-013 — simplify
- `toApiError` uses early returns instead of nested conditionals. `App.tsx` is a minimal shell (heading + subtitle); T-014 and T-015 add the components. `index.css` defines color tokens with a dark-mode variant.
- Gates: `npm run typecheck` → 0 errors; `npm run lint` (`--max-warnings 0`) → clean; `npm test` → 7/7; `vite build` → 220 kB JS (68.7 kB gzip). `npm install` → 0 vulnerabilities.
- Status: **done**.

### T-014 — red
- Tests: `DivisorSettings.test.tsx` (5): loads the current value into an input labelled "Special-case divisor"; saving 5 PUTs `{divisor:5}` and shows a status "Divisor saved: 5" (AC-025); a server 400 shows the ProblemDetails text in an alert (AC-026); value 1 shows the "every transaction will get random change" warning (spec review note); a failed load shows an alert.
- Stub component (`<section />`) → 5 failed / 7 passed. Example: `Unable to find a label with the text of: Special-case divisor`.

### T-014 — green
- `DivisorSettings`: loads via `getDivisor` (cancel-safe effect); a labelled number input (`useId`); Save → `setDivisor(Number(value))` → a `role="status"` confirmation; `ApiError.message` shown in `role="alert"`; a warning when the value is 1.
- First run 11/12: `[AC-026]` could not find the alert. The input's `min={1}` made the browser's built-in form validation block submitting 0, so the server's `Invalid divisor` ProblemDetails was never shown. **Fix:** removed `min` so the server is the single validator (as designed) and users see its message. 12/12.

### T-014 — refactor
- Lint (`@typescript-eslint/no-deprecated`): React 19.3 deprecates `FormEvent`, so switched to `SubmitEvent<HTMLFormElement>`.
- `App.tsx` renders `<DivisorSettings />`.

### T-014 — simplify
- Named `everyAmountDivisor = 1` for the warning rule, and a small `messageOf(error)` for the two error paths.
- Gates: `npm test` 12/12; typecheck 0; lint 0 (`--max-warnings 0`); `vite build` OK.
- Status: **done**.

### T-015 — red
- Design: `FileUpload({ onUploaded })` reports a success; `UploadedFilesList({ refreshKey })` loads the list itself and reloads when the key changes; `App` bumps the key after each upload, so each component can be tested alone.
- `FileUpload.test.tsx` (3): Upload is disabled until a file is chosen; success shows "Processed input.txt: 3 lines, 1 with errors" and calls `onUploaded` (AC-027); a 400 `Too many lines` is shown in an alert and `onUploaded` is not called (AC-023).
- `UploadedFilesList.test.tsx` (6): loads on mount in server order, newest first (AC-027); each row has a `download` link to `/api/files/{id}/output` named "Download change for <file>" (AC-028); reloads on a `refreshKey` change (AC-027); empty state; load error alert; column headers.
- Stubs → 9 failed / 12 passed.

### T-015 — green
- `FileUpload`: a labelled file input (`accept` .txt/.csv); Upload is disabled until a file is chosen or while uploading; on success shows a `role="status"` summary, clears the input and calls `onUploaded`; `ApiError.message` is shown in `role="alert"`.
- `UploadedFilesList`: a cancel-safe load keyed on `refreshKey`; empty state; error alert; a table labelled "Uploaded files" (File, Uploaded as a `<time>` formatted with `Intl.DateTimeFormat`, Lines, Errors, Output) with a `download` link per row, labelled "Download change for <file>".
- 21/21 on the first green run.

### T-015 — refactor
- `App.tsx` holds a `filesVersion` counter, bumped by `onUploaded` and passed to the list as `refreshKey`.
- **Plan change:** `web/src/index.css` was added to T-015's `files_in_scope` (`04-tasks.md` + `.tdd-state.json`) before editing, for table and form styles. `.table-scroll` gives the table horizontal scroll on narrow screens so the page never scrolls sideways.

### T-015 — simplify
- Renamed the `describe()` helper in `FileUpload.tsx` to `summaryText()`; the old name read like the test-framework global.
- Gates: `npm test` 21/21; typecheck 0; lint 0 (`--max-warnings 0`); `vite build` → 225 kB JS (70.4 kB gzip), 1.7 kB CSS.
- Status: **done**.
