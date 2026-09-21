# Design: 2026-09-21-cash-register-change-calculation

> Owner: `dotnet-architect` · Phase 3 · Template: `.claude/templates/design.template.md`
>
> **No invention.** Decisions not stated by the user or spec are captured as ADRs under `adr/`.

## Inputs

- `01-spec.md` revision: working tree 2026-09-21, revision 2 (28 active ACs, DC-001 to DC-003, NFR-001 to NFR-005)
- `02-spec-review.md` verdict: **PASS**
- Stack snapshot (greenfield; no `.specs/_stack.json` or `_onboarding.md`):
  - SDK: .NET 10.0.401 (`global.json` pins `10.0.100`, `rollForward: latestFeature`)
  - Language: C# 14, nullable enabled, warnings as errors
  - Web host: ASP.NET Core 10 Minimal API
  - Persistence: none. In-memory for v1 (ADR-002), so no EF Core and no migrations.
  - Tests: xUnit v3 on Microsoft.Testing.Platform, Shouldly, NSubstitute, `Microsoft.AspNetCore.Mvc.Testing`, ArchUnitNET
  - Front end: Vite + React 19 + TypeScript (strict), Vitest, React Testing Library, MSW (ADR-006)
  - Node 24 / npm 11

## Architecture Overview

One `CashRegister` module (class library) contains the business logic and its Minimal API endpoint mappings, organised as two slices: `Features/Currencies` and `Features/Change` (DC-003, ADR-001). A thin `CashRegister.Api` host composes the module and adds cross-cutting concerns: ProblemDetails, health, CORS and logging.

Money is always held as integer minor units (cents). The owed/paid pair is parsed per currency (AC-029, ADR-004).

Change is computed by a `ChangeCalculator`:
- it evaluates an ordered set of `IChangeRule`s (lowest `Priority` wins, AC-024);
- each rule names an `IChangeStrategy`;
- when no rule matches, the minimal strategy is used (AC-003).

The divisor rule reads the in-memory `IDivisorSettings`, which the UI changes through `PUT /api/settings/divisor` (AC-025). New rules and currencies are added by registering new classes; nothing existing changes (DC-001, DC-002, ADR-003).

Processed files are kept in an in-memory `IUploadedFileStore` for listing and download (AC-027, AC-028, ADR-002). A Vite React SPA in `web/` calls the API through a dev proxy.

## ADRs

- ADR-001: Single `CashRegister` module + thin API host; replace the earlier multi-project scaffold — accepted
- ADR-002: In-memory state for the divisor and uploaded files; no database or EF Core in v1 — accepted
- ADR-003: Change rules + strategies pipeline, integer minor units, random algorithm — accepted
- ADR-004: Currency registry; v1 processes files in USD only — accepted
- ADR-005: v1 security posture: no authentication, CORS limited to the SPA origin, request size limit — accepted
- ADR-006: Vite React SPA, not Next.js — accepted
- ADR-007: Output file format and line error message wording — accepted
- ADR-008: Observability v1: LoggerMessage logs + health check; OpenTelemetry deferred — accepted

## Solution Layout

```
CashRegister.slnx
global.json · Directory.Build.props · Directory.Packages.props · .editorconfig
src/
  CashRegister/                       ← the module (Microsoft.NET.Sdk + FrameworkReference Microsoft.AspNetCore.App)
    CashRegisterModule.cs             ← AddCashRegister(IServiceCollection) / MapCashRegister(IEndpointRouteBuilder)
    Features/
      Currencies/
        Denomination.cs  Currency.cs  UsdCurrency.cs  ICurrencyRegistry.cs  CurrencyRegistry.cs
      Change/
        Money/          Transaction.cs  ChangeLine.cs
        Strategies/     IChangeStrategy.cs  MinimalChangeStrategy.cs  RandomChangeStrategy.cs
        Rules/          IChangeRule.cs  OwedDivisibleByRule.cs  IDivisorSettings.cs  InMemoryDivisorSettings.cs
        ChangeCalculator.cs
        Formatting/     ChangeFormatter.cs
        Parsing/        TransactionLineParser.cs  LineParseResult.cs  LineErrors.cs
        Processing/     ChangeFileProcessor.cs  ProcessedFile.cs  CashRegisterLog.cs
        Files/          IUploadedFileStore.cs  InMemoryUploadedFileStore.cs  UploadedFile.cs  FileEndpoints.cs  FileDtos.cs
        Settings/       DivisorEndpoints.cs  DivisorDtos.cs
  CashRegister.Api/
    Program.cs  appsettings.json  appsettings.Development.json
tests/
  CashRegister.Tests/               ← unit + architecture tests, mirrors src/CashRegister/Features/...
  CashRegister.IntegrationTests/    ← WebApplicationFactory<Program> tests, [Trait("Category","Integration")]
web/                                ← Vite + React + TS
```

The first-pass `src/CashRegister.Domain`, `src/CashRegister.Application` and the three `*.Tests` projects are removed in T-001 (ADR-001).

## Component Map

| Slice | Visibility | Component | Responsibility | ACs |
|---|---|---|---|---|
| Currencies | public | `Denomination` | value in minor units, singular/plural name, `NameFor(count)` | AC-009, AC-010 |
| Currencies | public | `Currency` (abstract) / `UsdCurrency` | code, decimal separator, minor-unit digits, denominations ordered by value descending | AC-007, AC-029, DC-002 |
| Currencies | public | `ICurrencyRegistry` / `CurrencyRegistry` | resolves a currency from all registered `Currency` instances | DC-002 |
| Change | public | `Transaction` | owed/paid in minor units + currency; `ChangeDue` | AC-005 |
| Change | public | `IChangeStrategy`, `MinimalChangeStrategy` | greedy fewest pieces | AC-003 |
| Change | public | `RandomChangeStrategy` | random counts with an exact total; `Random` injected | AC-004, AC-005 |
| Change | public | `IChangeRule { int Priority; Type StrategyType; bool Matches(Transaction) }` | extension point for special cases | AC-024, DC-001 |
| Change | internal | `OwedDivisibleByRule` | `owed % divisorSettings.Current == 0`, priority 100 | AC-004, AC-013 |
| Change | public | `IDivisorSettings` / `InMemoryDivisorSettings` | thread-safe get/set, default 3, rejects < 1 | AC-025, AC-026 |
| Change | internal | `ChangeCalculator` | lowest-priority matching rule → strategy, else minimal | AC-003, AC-024 |
| Change | internal | `ChangeFormatter` | `3 quarters,1 dime,3 pennies`, `No change` | AC-006–AC-012, AC-016 |
| Change | internal | `TransactionLineParser` | `owed,paid` → `Transaction` or line error | AC-017–AC-020, AC-029 |
| Change | internal | `ChangeFileProcessor` | stream → ordered output lines; skips blanks; line limit | AC-001, AC-002, AC-021–AC-023 |
| Change | internal | `IUploadedFileStore` / `InMemoryUploadedFileStore` | concurrent dictionary of processed files | AC-027, AC-028 |
| Change | internal | `FileEndpoints`, `DivisorEndpoints` | Minimal API mappings with `TypedResults` | AC-015, AC-023, AC-025–AC-028 |

## Module Boundaries

- The `CashRegister` module's public surface is `CashRegisterModule.AddCashRegister()` / `MapCashRegister()` plus the extension-point types (`Currency`, `Denomination`, `IChangeRule`, `IChangeStrategy`, `Transaction`, `ChangeLine`, `IDivisorSettings`). Everything else is `internal` (with `InternalsVisibleTo` for the test projects).
- `Features/Currencies` depends on nothing in `Features/Change`.
- `Features/Change` depends on `Features/Currencies` only through `Currency`, `Denomination` and `ICurrencyRegistry`.
- `CashRegister.Api` references only `CashRegister` and calls only `AddCashRegister` / `MapCashRegister`.

ArchUnitNET rules added in T-012:
1. `Features.Currencies` must not depend on `Features.Change`.
2. `CashRegister` (module) must not depend on `CashRegister.Api`.
3. Every class implementing `IChangeRule` must be `sealed` and live in `Features.Change.Rules` (DC-001).
4. No type in the module calls `DateTime.Now`/`UtcNow`; use `TimeProvider`.
5. Types in `Features/*` must not reference `Microsoft.AspNetCore.Http` except types whose name ends in `Endpoints`.

## Entity Relationship Model

| Entity | Purpose | Key attributes | Relationships | Persistence notes |
|---|---|---|---|---|
| Transaction | one parsed line | owed, paid (long minor units), currency | 0..1 Change | transient |
| ChangeLine | count of one denomination | denomination, count | *..1 Denomination | transient |
| Denomination | coin or note | value, singular, plural | *..1 Currency | static per currency class |
| Currency | monetary system | code, decimal separator, minor digits | 1..* Denomination | DI singleton |
| Change rule | special case | priority, strategy, condition | 0..* Transaction | DI singleton |
| Divisor setting | active divisor | value ≥ 1, default 3 | drives OwedDivisibleByRule | in-memory singleton (ADR-002) |
| Uploaded file | processed file | id (Guid), file name, uploaded at, line count, error count, output text | 1..1 output | in-memory `ConcurrentDictionary` (ADR-002) |

## API Sketch (Minimal API, OpenAPI via `Microsoft.AspNetCore.OpenApi` at `/openapi/v1.json`)

```yaml
paths:
  /api/files:
    post:   # multipart/form-data, field "file"
      responses:
        '201': { UploadedFileSummary, Location: /api/files/{id} }       # AC-027
        '400': { ProblemDetails }  # no file / empty name / > 1000 non-blank lines (AC-015, AC-023)
    get:
      responses:
        '200': { UploadedFileSummary[] (newest first) }                  # AC-027
  /api/files/{id}/output:
    get:
      responses:
        '200': { text/plain; Content-Disposition: attachment; filename="<name>-change.txt" }  # AC-028
        '404': { ProblemDetails }
  /api/settings/divisor:
    get:  { '200': { divisor: int } }
    put:  # body { divisor: int }
      responses:
        '200': { divisor: int }                                         # AC-025
        '400': { ProblemDetails / HttpValidationProblemDetails }        # AC-026, AC-015
  /health:  { '200': Healthy }                                          # NFR-003
```

`UploadedFileSummary = { id, fileName, uploadedAt, lineCount, errorLineCount }`.

Returns are declared as `Results<Created<UploadedFileSummary>, ProblemHttpResult>`, `Results<FileContentHttpResult, NotFound<ProblemDetails>>` and so on. Divisor validation uses .NET 10 built-in Minimal API validation (`AddValidation()` + `[Range(1, int.MaxValue)]`). A non-integer JSON body fails binding, which yields a 400 ProblemDetails through `AddProblemDetails()`.

## Processing Flow

1. The endpoint reads the `IFormFile` stream.
2. `ChangeFileProcessor.Process(stream, currency)` reads line by line and skips whitespace-only lines (AC-022). Once more than 1000 non-blank lines are seen, it returns `LineLimitExceeded` and stops reading (AC-023).
3. For each line, `TransactionLineParser`:
   - returns a `Transaction`, or `Error: …` text (AC-017–AC-020) and processing continues (AC-021);
   - otherwise `ChangeCalculator` produces the lines and `ChangeFormatter` renders them (`No change` when due is 0, AC-016).
4. The output joins lines with `\n` in input order (AC-001, AC-002; ADR-007), is stored in `IUploadedFileStore` with `TimeProvider.GetUtcNow()`, a log event is written (NFR-002), and the endpoint returns 201.

Parsing (USD, ADR-004): the field separator is `,`; exactly 2 fields after trimming; the decimal separator comes from the currency (`.` for USD); at most `MinorUnitDigits` decimal places (2); no thousands separators or currency symbols; `-` gives the negative-amount error (AC-018).

## Line Error Messages (ADR-007)

| Condition | Output line |
|---|---|
| not exactly two fields / not a number | `Error: invalid line, expected '<owed>,<paid>'` |
| negative amount | `Error: amounts must not be negative` |
| paid < owed | `Error: amount paid is less than amount owed` |
| too many decimals | `Error: amounts must have at most 2 decimal places` |

## Data Model + Migrations

- No database in v1 (ADR-002): no EF Core, no `DbContext`, no migrations. The persistence layer is deferred to v2 ("persist in db", "N days"). The store interfaces are the seam.

## ProblemDetails Error Model

- `builder.Services.AddProblemDetails()`, `app.UseExceptionHandler()`, `app.UseStatusCodePages()`. Unhandled exceptions return 500 ProblemDetails with no stack trace outside Development.
- Endpoint failures use `TypedResults.Problem(title, detail, statusCode)` with stable titles: `Invalid file`, `Too many lines`, `File not found`, `Invalid divisor`.
- Per-line errors are data in the output, not ProblemDetails (spec AC-015 vs Q-005).

## Security Posture (ADR-005)

- AuthN/AuthZ: none in v1 (NFR-004); endpoints are anonymous. JWT arrives in v2.
- CORS: a named policy allowing only the origins in `Cors:AllowedOrigins` (Development: `http://localhost:5173`). Production serves the SPA behind the same origin.
- Request limits: `FormOptions.MultipartBodyLengthLimit` and the upload endpoint's `RequestSizeLimit` are 1 MB. 1000 lines × a generous 64 bytes ≈ 64 KB, so 1 MB guards memory without rejecting valid files.
- Input: file content is treated as data only; the file name is sanitised for `Content-Disposition` (path chars stripped, max 100 chars).
- PII: none. Secrets: none.

## Observability (ADR-008)

- `CashRegisterLog` (LoggerMessage source-generated):
  - `FileProcessed(Guid fileId, string fileName, int lineCount, int errorLineCount, long elapsedMs)`, Information (NFR-002)
  - `FileRejected(string fileName, string reason)`, Warning
  - `DivisorChanged(int oldValue, int newValue)`, Information
- `builder.Services.AddHealthChecks()` → `app.MapHealthChecks("/health")` (NFR-003).
- JSON console logging in non-Development environments.

## Front End (ADR-006)

`web/src/`:
- `api/client.ts`: typed `fetch` wrappers `uploadFile`, `listFiles`, `outputUrl(id)`, `getDivisor`, `setDivisor`; ProblemDetails is parsed into a `ApiError`.
- `features/divisor/DivisorSettings.tsx`: number input with a "Save" button; shows server ProblemDetails errors and warns when the value is 1 (AC-025, AC-026).
- `features/files/FileUpload.tsx`: file input and upload; shows the ProblemDetails error (AC-023).
- `features/files/UploadedFilesList.tsx`: table (name, uploaded at, lines, errors) with a Download button per entry that opens `outputUrl(id)` (AC-027, AC-028).
- `App.tsx` composes them; `vite.config.ts` proxies `/api` and `/health` to the API.

## Risks + Rollback

| Risk | Likelihood | Impact | Mitigation | Rollback |
|---|---|---|---|---|
| The in-memory file store grows without bound | medium | memory | 1 MB request cap, 1000 lines; restart clears it (NFR-005); v2 adds retention | restart the process |
| Multiple API instances diverge on divisor/files | low (v1 single instance) | inconsistent UX | document single-instance deployment in ADR-002 | n/a |
| A future currency uses `,` as its decimal separator, which clashes with the `,` field separator | medium (v2 EUR) | parse ambiguity | ADR-004: the field separator becomes per-currency (e.g. `;`) when such a currency is added | n/a for v1 |
| Divisor = 1 makes every line random | low | surprise | UI warning (spec review note) | set the divisor back to 3 |
| p95 regression | low | NFR-001 | integration perf test (T-012) | revert the offending commit |

## Non-Functional Requirements

- NFR-001: p95 < 500 ms for a 1000-line upload. Verified by `FileUploadPerformanceTests` (50 sequential uploads in-process after warm-up; the 95th percentile is asserted).
- NFR-002: `FileProcessed` log event. Verified with `FakeLogger` in a unit test.
- NFR-003: `/health` returns 200. Integration test.
- NFR-004: no auth. Integration tests call without credentials.
- NFR-005: in-memory state. By construction (ADR-002).

## Open Questions

- (none)

## Resolved Questions

- (none; all design choices beyond the spec are recorded as ADRs for user review)

## Sign-off

- [x] Every AC from `01-spec.md` is addressed by at least one component or task (see the coverage table in `04-tasks.md`).
- [x] All `Q-NNN` resolved or deferred-with-rationale.
- [ ] Reviewed by user on <YYYY-MM-DD>.
