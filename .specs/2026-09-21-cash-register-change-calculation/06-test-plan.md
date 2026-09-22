# Test Plan: 2026-09-21-cash-register-change-calculation

> Owner: `dotnet-test-engineer` · Phase 5 · Template: `.claude/templates/test-plan.template.md`

## Inputs

- `04-tasks.md` revision: `fcb7a26` (all 16 tasks done)
- `05-implementation-log.md` revision: `fcb7a26`
- Stack snapshot:
  - .NET 10 · xUnit v3 4.0.1 on Microsoft.Testing.Platform · Shouldly · NSubstitute · FakeLogger · ArchUnitNET 0.13.4 · `WebApplicationFactory`.
  - No database, so no Testcontainers (ADR-002).
  - Web: Vitest 5 (happy-dom) · React Testing Library · MSW 2.
  - OpenAPI source: generated from the Minimal API endpoints (`Microsoft.AspNetCore.OpenApi`, Development only).

## Suites

| Suite | Project / folder | Count | Gate |
|---|---|---|---|
| Unit | `tests/CashRegister.Tests` | 128 | `unit` |
| Architecture (ArchUnitNET) | `tests/CashRegister.Tests/Architecture` | 8 (included in the 128) | `unit` |
| Integration (`WebApplicationFactory`, `[Trait("Category","Integration")]`) | `tests/CashRegister.IntegrationTests` | 30 | `it` |
| Performance (NFR-001, `[Trait("NFR","NFR-001")]`) | `tests/CashRegister.IntegrationTests/Performance` | 1 (included in the 30) | `it` |
| Web unit/component (Vitest + RTL + MSW) | `web/src/**/*.test.ts(x)` | 30 | `web-unit`, `web-coverage` (`npm run test:coverage`, 90% floor) |

There are no containers. The integration suite runs the real API host in process. The divisor tests start a fresh host per test because the divisor is process-wide state.

## AC × test scope

U = unit · I = integration (full host) · W = web component/client · P = performance. Counts are tagged tests; the full list is in `07a-traceability.md`.

| AC | Behaviour | U | I | W | Notes |
|---|---|---|---|---|---|
| AC-001 | one output line per non-blank input line | ✓ | ✓ | | also P (NFR-001) |
| AC-002 | output keeps input order | ✓ | | | |
| AC-003 | no rule matches → minimal change | ✓ | | | |
| AC-004 | owed divisible by divisor → random | ✓ | | | seeded `Random` |
| AC-005 | denominations sum exactly to change | ✓ | | | property-style loops: 0..1000¢ minimal; 1000 seeds random |
| AC-006 – AC-010 | format, order, zero omission, singular/plural | ✓ | | | |
| AC-011, AC-012 | README samples 1 and 2 | ✓ | ✓ | | through the formatter, the processor and the download endpoint |
| AC-013 | active divisor N drives the rule | ✓ | | | |
| AC-015 | whole-request failures → ProblemDetails | | ✓ | ✓ | 400 / 404 / 500 (500 hides internals) |
| AC-016 | `No change` | ✓ | | | |
| AC-017 – AC-020 | per-line error messages | ✓ | | | exact ADR-007 wording |
| AC-021 | invalid line doesn't stop processing | ✓ | | | |
| AC-022 | blank lines ignored | ✓ | | | |
| AC-023 | > 1000 lines rejected | ✓ | ✓ | ✓ | 1000 OK / 1001 rejected |
| AC-024 | lowest priority wins | ✓ | | | incl. ties; also arch: rules sealed, slices cycle-free |
| AC-025 | UI divisor change applies to later files | ✓ | ✓ | ✓ | deterministic IT: after divisor 5, 3.33 → exact minimal |
| AC-026 | divisor < 1 rejected | ✓ | ✓ | ✓ | |
| AC-027 | uploaded-files list | ✓ | ✓ | ✓ | also OpenAPI smoke |
| AC-028 | download output | ✓ | ✓ | ✓ | `Content-Disposition` attachment |
| AC-029 | separator from currency | ✓ | | | test currency with `'` separator |

## Cross-cutting suites

### Architecture (ArchUnitNET), `ArchitectureTests`

- Currencies must not depend on Change, with a positive control proving the reverse dependency is visible.
- Feature slices are free of cycles (`Slices().Matching("CashRegister.Features.(*)..")`), **added in this phase**.
- The module does not reference the API host.
- `IChangeRule` implementations are sealed and live in `Features.Change.Rules` (DC-001).
- No `DateTime(Offset).Now/UtcNow` calls; time comes from `TimeProvider`.
- Only `*Endpoints` types use `Microsoft.AspNetCore.Http`, with a positive control. The ASP.NET Core assemblies are loaded so this rule can actually fail (found in T-012).
- N/A: "no EF Core types on the API surface", because there is no EF Core (ADR-002).

### Contract (OpenAPI)

- `OpenApiDocumentTests`, **added in this phase**: `/openapi/v1.json` lists `GET/POST /api/files`, `GET /api/files/{id}/output` and `GET/PUT /api/settings/divisor`.
- `ProductionHostTests.Production_DoesNotExposeTheOpenApiDocument`, **added in this phase**: the document is not served outside Development.
- No full-document snapshot: Gap-004 was dropped by the user because of Verify's licensing requirement.

### Host behaviour outside Development, `ProductionHostTests` (**added in this phase**)

- An unhandled exception gives 500 `application/problem+json` without the exception message or type (design, ProblemDetails model).
- No CORS origins are allowed by default (ADR-005).

### Property-style tests

- Both strategies are checked over input ranges with plain loops: minimal change for 0..1000¢; random change for 1000 seeds × 0..500¢. Each asserts the exact-sum invariant (AC-005), and random change also checks largest-first ordering with no zero counts. See Gap-006 for a library-based alternative.

## Coverage

- Floor 90% line + branch; target 95%+; new code 95%.
- Merged unit + integration run (our source only; generated `obj/` files excluded), measured 2026-09-21 after this phase: **line 435/435 = 100%**, **branch points 63/64**.
- The two projects write separate reports, `artifacts/coverage/unit.xml` and `artifacts/coverage/it.xml`, because they would otherwise overwrite each other (T-009 note). `/net-validate` must merge them.
- Web (Vitest v8): 100% lines, statements and functions; 90.38% branches; gated at 90% (Gap-007).

## Mutation

- Stryker.NET is not configured (no `stryker-config.json`), so the `mutation` gate is **skipped**, which is not a gap. Candidates if opted in: `ChangeCalculator`, `OwedDivisibleByRule`, `TransactionLineParser`, `ChangeFormatter`.

## Gaps + waivers

| ID | Gap | Resolution |
|---|---|---|
| Gap-001 | `Program.cs` 7–10: JSON console logging outside Development never ran | **Closed**: `ProductionHostTests` runs the host as Production |
| Gap-002 | `Program.cs` 17: 500 branch of `StatusCodeSelector` never ran | **Closed**: `UnhandledException_Returns500ProblemDetailsWithoutInternalDetails` |
| Gap-003 | `Program.cs` 21: `?? []` when `Cors:AllowedOrigins` is absent | **Closed by T-018** (2026-09-21): the fallback moved to `HostSetup.AllowedOrigins`, which `HostSetupTests.AllowedOrigins_MissingSection_IsEmpty` exercises directly. The original won't-fix rationale is kept below for history. ~~Won't fix.~~ `appsettings.json` always declares the key, and an empty JSON array binds to an empty array, not null. The fallback only protects against someone deleting the key; reaching it would mean a test-only config that removes shipped settings. |
| Gap-004 | No Verify snapshot of `openapi.json` | **Blocked on a licensing decision.** Approved by the user on 2026-09-21, but `Verify.XunitV3` 33.1.1 fails the build with SponsorCheck **SC021**: Verify's maintenance-fee policy requires a declared sponsorship, a licence, an exemption (e.g. `OpenSource`, `SmallRevenue`) or an explicit breach acknowledgement. The package was removed so the build stays green. **Won't fix (user decision, 2026-09-21):** Gap-004 is dropped; `OpenApiDocument_DescribesEveryPublicEndpoint` (every path + method present) stays as the contract guard. |
| Gap-005 | `.github/scripts/traceability-dotnet.sh` (and the rest of the harness scripts) are not installed | `07a-traceability.md` was generated by a stand-in script with the same inputs. **Recommend `/net-wire-harness`** before `/net-validate`, which expects `artifacts/harness-summary.json`. |
| Gap-006 | Property-based tests use loops, not a generator library (FsCheck) | **Won't fix for v1.** The input domain is small and fully enumerated for minimal change (0..1000¢) and dense for random change (1000 seeds). FsCheck would be a new package. |
| Gap-007 | Web coverage not collected | **Closed** (approved 2026-09-21): `@vitest/coverage-v8@5.0.1` added, plus `npm run test:coverage` (Cobertura to `artifacts/coverage/web/`) with 90% thresholds on lines, branches, functions and statements. The first run failed at **78.84% branches** (`App.tsx` never rendered; network-error, no-title and stale-response branches untested). 9 tests were added (web 21 → 30). Now **100% lines / statements / functions, 90.38% branches (47/52)**. The 5 remaining branches are the `cancelled` guards in `DivisorSettings`' load, the `inputRef` null check, the `?? null` for an empty file selection in `FileUpload`, and one `cancelled` guard in `UploadedFilesList`. They are defensive checks for React teardown timing that tests cannot trigger reliably. |
