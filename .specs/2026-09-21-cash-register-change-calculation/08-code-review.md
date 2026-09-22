# Code Review: 2026-09-21-cash-register-change-calculation

> Owner: `dotnet-code-reviewer` · Phase 7 · Skills: `dotnet-code-review-rubric`, `clarity-over-cleverness`, `dotnet-security-baseline`, `dotnet-observability-ops`
>
> Pre-commit. The reviewer reports and does not repair: no code, test or config was changed during this review.

## Inputs

- Spec: `01-spec.md` (28 active ACs) · Design: `03-design.md`, ADR-001 to ADR-009
- Validation: `07-validation-report.md` (**PASS**, `b135f84`) · `07a-traceability.md` (28/28, 0 orphans)
- **Diff: `9b945d3...HEAD`**, the whole feature. The default base `origin/main` (`4423547`) already contains T-001 to T-012, which were pushed without a review; reviewing only since then would skip most of the backend. Reviewable scope, excluding `.specs/` and `.claude/`: 103 files, about 9.4k lines (including `web/package-lock.json`).
- Evidence gathered read-only: mechanical greps (NoWarn, pragma, inline versions, null-forgiving, integration traits, Moq/Assert/Sleep), plus one live probe of the built API (oversize and wrong-content-type uploads; see F-002).

## Rubric application

### 1. Traceability — ✅
- All 28 active ACs have tagged tests (`[Trait("AC", …)]` in C#, `[AC-NNN]` in Vitest names); 0 orphan tags. AC-014 is withdrawn and covered by DC-001 through the architecture rules.
- Every class in `tests/CashRegister.IntegrationTests` tags every test with `[Trait("Category","Integration")]` (checked mechanically).

### 2. Slice and module boundaries — ✅
- One module with `Features/Currencies` and `Features/Change`; the host only composes (ADR-001). ArchUnitNET enforces: no Currencies → Change dependency, slices cycle-free, module doesn't reference the host, `IChangeRule` implementations sealed and in `Rules`, ASP.NET Core HTTP types only in `*Endpoints`. Each negative rule has a positive control.

### 3. Minimal API idioms — ⚠️ F-001 (major), F-003 (minor)
- ✅ `TypedResults` everywhere, with typed unions (`Results<Created<…>, ProblemHttpResult>`, `Results<FileContentHttpResult, ProblemHttpResult>`); per-slice `MapFileEndpoints` / `MapDivisorEndpoints`; static handlers taking their dependencies as parameters; no controllers and no service locator.
- ❌ `Program.cs` contains branching and expression logic (F-001).
- ⚠️ Validation sits inside handlers rather than an endpoint filter or built-in validation (F-003).

### 4. Error handling — ✅ with F-002 (minor)
- Every failure path returns ProblemDetails. Checked live: 400 bad JSON, 404, 415 wrong content type, 500 without internal details (tested), and an oversize upload returns 400 ProblemDetails. No `ex.Message` reaches clients. Per-line errors are data by design (ADR-007).
- The oversize-upload response is a generic "Bad Request" with no detail and no test (F-002).

### 5. Data access — n/a
- No EF Core and no database (ADR-002). The in-memory store is unbounded, which is accepted in ADR-002 (bounded per request by the 1 MB / 1000-line limits; lost on restart).

### 6. Nullable discipline — ✅
- Nullable is on solution-wide, with CS8600/8602/8618 as errors. There are two `!` in production code, each with a justification comment on the line above: `CashRegisterModule.cs:38` (the options validator guarantees the code is registered) and `ChangeFileProcessor.cs:57` (`LineParseResult` guarantees an error when there is no transaction). `ChangeDivisorRequest.Divisor` is deliberately `int?`, so a missing value is reported rather than read as 0.

### 7. Async correctness — ✅
- The `CancellationToken` flows from the upload handler through `ChangeFileProcessor.ProcessAsync` into `StreamReader.ReadLineAsync`. There is no sync-over-async and no `async void`. CA2016 (forward the token) is an error.

### 8. Security — ✅ (waived items listed)
- Input is validated at the boundary: multipart field required, 1 MB form limit, 1000-line cap, strict amount parser, divisor ≥ 1. File names are stripped of paths and invalid characters and capped at 100 characters before use in `Content-Disposition` or logs. There are no secrets in code or config. CORS is an allow-list from config (development origin only). 0 vulnerable NuGet/npm packages; NuGet audit runs at build.
- **Waived by ADR-005:** no authentication/authorization, antiforgery disabled on the upload, no rate limiting (v1 internal tool; JWT in v2).

### 9. Test quality — ⚠️ F-006, F-007 (minor)
- ✅ Shouldly only; NSubstitute only (no Moq); no skipped tests; no `Thread.Sleep` / `Task.Delay` in C# tests; deterministic time (a `TimeProvider` fake); the random strategy is tested with seeded `Random`. The red excerpts in `05-implementation-log.md` show real failures, and the compile-stage reds are disclosed as such.
- ✅ Narrowest scope: handler logic unit-tested directly (T-017); `WebApplicationFactory` used only for host/HTTP behaviour; no containers.
- ⚠️ Timer waits in web tests (F-006). One C# test checks five behaviours (F-007).
- **Waived by ADR-009:** `HostCompositionTests` runs a real in-process host inside the unit gate (user decision).

### 10. Clarity over cleverness — ✅ with F-004, F-005
- Domain names follow the glossary (`Transaction`, `ChangeLine`, `Denomination`, `OwedDivisibleByRule`, `NoChange`); guard clauses rather than nested conditions; literals named (`MaxLines`, `DefaultPriority`, `EntrySeparator`, `MinimumDivisor`). The design document has drifted from the code in two places (F-004).

### 11. Packaging and dependencies — ✅
- Central package management with transitive pinning; no inline `Version=`; no `NoWarn`; no `#pragma warning disable`. The SDK is pinned (`latestFeature`, no prerelease). npm dependencies are locked (`package-lock.json`). **Waived by ADR-009:** Meziantou.Analyzer and XML docs.

## Findings

| ID | Severity | Section | File | Line | Finding | Suggested fix |
|----|---------|---------|------|------|---------|---------------|
| F-001 | **major** | minimal-api | `src/CashRegister.Api/Program.cs` | 7–10, 16–17, 21, 33–36 | `Program.cs` contains logic: two `IsDevelopment()` branches, a ternary status-code selector, and a `?? []` fallback. The rubric makes any `if` or expression in `Program.cs` a major, because it can only be tested by booting the host (which is why `HostCompositionTests` had to exist). | Move the composition into testable extension methods in the host project, e.g. `builder.AddCashRegisterHost()` (JSON logging choice, ProblemDetails + `BadHttpRequestStatus` selector, CORS from config, health, OpenAPI) and `app.UseCashRegisterHost()` (pipeline + Development-only OpenAPI), leaving `Program.cs` as build → use → map → run. Unit-test the selector (`BadHttpRequestException` → its status; other → 500) as a plain function. Alternatively, record an ADR waiving this rule for the environment switches. |
| F-002 | minor | error-handling | `src/CashRegister/Features/Change/Files/FileEndpoints.cs` | 29 | An upload over the 1 MB `multipartBodyLengthLimit` returns **400 "Bad Request" with no detail** (observed live with a 2.1 MB file), so the UI can only show "Bad Request". There is no test for this path. | Map the oversize case to **413** `ProblemDetails` titled "File too large" with a detail naming the 1 MB limit (e.g. via the exception handler's status selector for `BadHttpRequestException` 413, or by reading the form explicitly). Add an integration test for a file over 1 MB. |
| F-003 | minor | minimal-api | `src/CashRegister/Features/Change/Settings/DivisorEndpoints.cs` | 35 | Request validation (`Divisor` missing or < 1) runs inside the handler rather than an endpoint filter or .NET 10 built-in validation, which is what the rubric expects and what `03-design.md:151` says (`AddValidation()` + `[Range]`). | Either move the check to an endpoint filter (or `[Required]`/`[Range(1, int.MaxValue)]` + `AddValidation()`, keeping the "Invalid divisor" title), or keep the handler check and record the choice in an ADR and update the design (see F-004). The missing-file check in `FileEndpoints.cs:46` is binding-level and can stay. |
| F-004 | minor | clarity | `.specs/…/03-design.md` | 151, 194 | The design has drifted from the code: line 151 says divisor validation uses `AddValidation()` + `[Range]` (the code validates in the handler), and line 194 says `FileProcessed(Guid fileId, …)` (the code logs the file name, not an id; recorded in the T-008 log). | Update `03-design.md` to describe the as-built behaviour, pointing to the implementation-log entries. |
| F-005 | minor | clarity / tooling | `.github/scripts/lib/harness.mjs` | 27 | `spawnSync(..., { shell: true })` on Windows joins the arguments without quoting, so any repo or artifact path containing a space (e.g. `C:\Users\Jane Doe\…`) splits into two arguments and the gates fail with misleading errors. | Use `shell: false` and resolve the Windows executables explicitly (`npm.cmd`, `dotnet.exe`), or quote each argument when `shell` is on. |
| F-006 | minor | test-quality | `web/src/features/divisor/DivisorSettings.test.tsx`, `web/src/features/files/FileUpload.test.tsx`, `web/src/features/files/UploadedFilesList.test.tsx` | 109, 58, 102 | `await new Promise(r => setTimeout(r, 20))` is used as synchronisation: a time-based wait that can pass without the awaited work having happened (or flake on a slow CI agent). | Assert on an observable end state with `waitFor`, or resolve the MSW handler's promise and `await` the component's next render; for "nothing happens" checks, wait for the request counter explicitly. |
| F-007 | minor | test-quality | `tests/CashRegister.Tests/HostCompositionTests.cs` | 53–76 | One test checks five separate behaviours (health, bad JSON → 400, crash → 500, CORS, OpenAPI exposure), so the first failure hides the rest. | Split into one test per behaviour, each parameterised by environment where relevant. This becomes smaller anyway if F-001 moves the composition into testable methods. |
| F-008 | nit | concurrency | `src/CashRegister/Features/Change/Settings/DivisorEndpoints.cs` | 43–45 | Reading the old divisor and then writing the new one isn't atomic, so two concurrent PUTs can log a stale "old" value. The stored value itself is always correct. | Have `IDivisorSettings.Change` return the previous value (`Interlocked.Exchange`) and log that. |
| F-009 | nit | tooling | `.github/scripts/lib/harness.mjs` | 165 | The NuGet severity count matches the words `High`/`Critical` anywhere in `dotnet list` output (e.g. a package name containing "High" would count). | Use `dotnet list package --vulnerable --include-transitive --format json` and read the severities from the structured output. |
| F-010 | nit | spec | `src/CashRegister/Features/Change/Rules/OwedDivisibleByRule.cs` | 24 | An owed amount of `0.00` is divisible by every divisor, so it always gets random change. This is consistent with the spec (divisibility in cents) but not stated anywhere. | Add a glossary note in `01-spec.md` ("0 is divisible by any divisor, so owed 0.00 gets random change") so it reads as intended. |

## Praise

- **Positive-control architecture tests** caught a rule that could never fail (no ASP.NET Core types loaded) before it shipped (T-012).
- **Deterministic proof of AC-025** without leaning on randomness: after divisor 5, `3.33,5.00` must produce the exact minimal output.
- **The line parser reads as its ADR:** `Parse` is the ADR-007 check sequence, and number handling is isolated in `ParsedAmount`.
- **Honest logs:** compile-stage reds, test bugs, real bugs (500 on bad JSON, `min=1` hiding the server error) and plan changes are all recorded, with scope widened before edits.

## Waivers relied on (each has an ADR)

| Item | ADR |
|---|---|
| No authentication / antiforgery / rate limiting in v1 | ADR-005 |
| Unbounded in-memory store and divisor; single instance | ADR-002 |
| Meziantou.Analyzer and XML docs not adopted; `_camelCase` private fields; no `--no-build` | ADR-009 |
| `HostCompositionTests` runs a host inside the unit gate | ADR-009 |
| Mutation testing not opted in (gate `skipped`) | 06-test-plan.md, `_stack.json` (expected per the validation rules) |

## Summary

**1 major · 6 minor · 3 nit · 4 praise** (0 blockers). No `must-fix` beyond F-001.

## Resolution log

- **F-001 → resolved by T-018 (2026-09-21, user: "Fix it").** Host composition moved to `src/CashRegister.Api/HostSetup.cs` (`AddCashRegisterHost`, `UseCashRegisterHost`, plus `StatusCodeFor` and `AllowedOrigins` as unit-tested functions). `Program.cs` is now build → `AddCashRegisterHost` → `UseCashRegisterHost` → run, with no branches or expressions. 10 new unit cases (`HostSetupTests`); all 30 integration and host tests unchanged and green; unit coverage 100% line / 100% branch (this also closes test-plan Gap-003). F-002 to F-010 remain open as recommendations and do not block.
- **Verdict after resolution: ✅ Approve.** No blockers or majors remain; the minors and nits are noted above.

## Verdict (at review time)

❌ **Request changes.** F-001 is a rubric **major** with no covering ADR, so the commit gate is closed.

**Smallest recovery:** add a task **T-018 "Extract host composition from Program.cs"** (fixes F-001; also shrinks F-007) and run `/net-build T-018`. Alternatively, if you'd rather keep the environment switches in `Program.cs`, record an ADR waiving F-001; the verdict then becomes ⚠️ **Approve with waivers**.

The minors (F-002 to F-007) are recommended but don't block. F-002 and F-003 fit naturally in the same or a follow-up task; F-004 is a docs-only edit.
