---
name: dotnet-code-review-rubric
description: Pre-commit code review rubric for .NET 10 changes. Used by the .NET code reviewer to produce `08-code-review.md` before any commit. Covers traceability, slice boundaries, Minimal API idioms, error handling, data access, nullable discipline, async correctness, security, test quality, clarity, and packaging.
when_to_use:
  - Phase 7 (Code review) — the `/net-review` command.
  - Pre-PR review on a brownfield .NET repo where this toolkit is being adopted.
authoritative_references:
  - .claude/templates/code-review.template.md
  - .claude/skills/dotnet-10-conventions/SKILL.md
  - .claude/skills/dotnet-security-baseline/SKILL.md
  - .claude/skills/dotnet-coverage-and-mutation/SKILL.md
  - .claude/skills/dotnet-observability-ops/SKILL.md
  - .claude/skills/clarity-over-cleverness/SKILL.md
---

# .NET Code Review Rubric

## Severity ladder

- **blocker** — must fix before commit. (Security hole, broken behavior, gate-bypass.)
- **major** — must fix before commit OR document as ADR + waiver.
- **minor** — should fix; leave a note if not.
- **nit** — taste; mention once, don't insist.

## Eleven sections

### 1. Traceability

- Every diff hunk maps to an `AC-NNN` and a `T-NNN`.
- Files-in-scope honored — no edits outside the task's declared paths in `.tdd-state.json`.
- Every test carries `[Trait("AC","AC-NNN")]`; every integration test additionally carries
  `[Trait("Category","Integration")]` (the harness splits the `unit` and `it` gates on that
  filter, so a missing trait silently drops the test from a gate).
- Every AC in `01-spec.md` has at least one test that names it.
- The matrix in `07a-traceability.md` matches reality — spot-check three rows against the actual
  files; a stale matrix is `major`.
- No orphan test: a `[Trait("AC","AC-999")]` referencing an AC that does not exist.

### 2. Slice and module boundaries

- Production code lives under `src/<Module>/Features/<Slice>/`; tests mirror it under
  `tests/<Module>.Tests/Features/<Slice>/` and `tests/<Module>.IntegrationTests/`.
- **No cross-slice references.** `Features/Orders/*` must not reference a type from
  `Features/Payments/*`. Cross-slice communication goes through the module's public contract.
- No `internal` type reached across a module boundary via `InternalsVisibleTo` added in this diff.
- ArchUnitNET rules exist for the new slice and pass — layer direction, no cross-slice
  dependency, naming (`*Endpoint`, `*Handler`, `*Validator`).
- No new circular project reference; the solution graph is still a DAG.

### 3. Minimal API idioms

Apply `dotnet-10-conventions`. Look for:

- `TypedResults.*` returned, not `Results.*`, and the endpoint's return type is
  `Results<Ok<T>, ProblemHttpResult>` or similar — not bare `IResult`.
- Every error path returns ProblemDetails. No ad-hoc error envelope, no bare string body.
- Validation runs in an endpoint filter (`AddEndpointFilter<ValidationFilter>`), not inside the
  handler and not via `if (!ModelState.IsValid)`.
- **No logic in `Program.cs`** — it composes and maps. Any `if`, loop, or business expression
  there is `major`; extract to an extension method that can be tested.
- Endpoints registered via a per-slice `MapXxx(this IEndpointRouteBuilder)` extension, not a
  hundred inline lambdas in `Program.cs`.
- Handlers are static or take their dependencies as parameters; no service-locator
  `IServiceProvider.GetService` in a handler.
- Mapster mappings configured centrally; no hand-written 40-line mapper next to a generated one.
- No MVC controllers, `[ApiController]`, or `ActionResult<T>` added to a Minimal API codebase.

### 4. Error handling

- No swallowed exceptions: an empty `catch {}`, `catch (Exception) { }`, or a `catch` that only
  logs `Debug` and continues is `blocker`.
- No `catch (Exception ex) { throw new Exception(ex.Message); }` — that destroys the stack trace.
  Rethrow with bare `throw;` or wrap in a domain exception preserving the inner exception.
- **No leaked detail** — walk section 9 of `dotnet-security-baseline`. `ex.Message` in a response
  body is `blocker`.
- **Expected failures are modelled as results, not exceptions.** "Order not found", "insufficient
  stock", "already shipped" are outcomes, not exceptions; they return a result type the endpoint
  translates to 404/409/422. Exceptions are for the genuinely unexpected.
- Exception filters (`when (...)`) preferred over catch-and-rethrow.
- `CancellationToken` cancellation (`OperationCanceledException`) is not logged as an error.

### 5. Data access

- **No N+1** — navigation property access inside a `foreach` or a LINQ projection over a loaded
  collection. Fix with `Include`, a projection to a DTO in the query, or a single batched query.
- `AsNoTracking()` on every read-only query. A tracked read path is `minor`; a tracked read path
  in a hot endpoint is `major`.
- **No unbounded query.** Every list query has `Take(...)`, and every paginated endpoint enforces
  a server-side page-size ceiling regardless of the caller's value.
- Migrations reviewed as code: the generated `Up`/`Down` read, destructive operations flagged,
  no data loss without an ADR, no edit to an already-released migration.
- **No `EnsureCreated()`** anywhere — not in `Program.cs`, not in a test fixture. Migrations only.
- No interpolated string into `FromSqlRaw`/`ExecuteSqlRaw`; use the interpolated
  `FromSql`/`ExecuteSql` overloads which parameterise.
- `SaveChangesAsync` called once per unit of work, not per entity in a loop.
- No `DbContext` captured in a singleton, a static, or a long-lived closure.

### 6. Nullable-reference discipline

- `<Nullable>enable</Nullable>` still on in `Directory.Build.props`; no project opts out.
- **No `#nullable disable`** in the diff — `blocker` unless it is a generated file.
- **No `!` null-forgiving operator** without a comment on the same line explaining why null is
  impossible. An undocumented `!` is `major`; `!` used to silence a warning the author did not
  understand is `blocker`.
- No `CS86xx` warning suppressed via `#pragma warning disable` or `NoWarn`.
- Reference-type parameters that may be null are declared `T?`, not `T` with a runtime guard.
- `required` / primary-constructor parameters used instead of `= null!;` initialisers on
  non-nullable properties.

### 7. Async correctness

- **No sync-over-async**: `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`, `Task.WaitAll` on a
  request path is `blocker`.
- No `async void` except an event handler.
- **`CancellationToken` threaded through** every async call from the endpoint parameter down to
  the EF Core / `HttpClient` call. A dropped token is `major`.
- No `Task.Run` wrapping synchronous work inside a request.
- `ConfigureAwait(false)` on every `await` in a class library project.
- Unbounded fan-out (`Task.WhenAll` over a caller-supplied collection) is bounded by
  `MaxDegreeOfParallelism` or a `SemaphoreSlim`.
- `IAsyncEnumerable<T>` consumed with `await foreach` and a token, not materialised with
  `ToListAsync()` when the point was streaming.

### 8. Security

Walk the review-baseline table in `.claude/skills/dotnet-security-baseline/SKILL.md` row by row
and record each violation with its stated severity. Do not summarise it — the eighteen rows are
the checklist. At minimum, block on:

- An endpoint with no authorization decision recorded.
- A secret or credential in a committed file.
- `ex.Message` or a stack trace in a response.
- `AllowAnyOrigin()` with credentials.
- SQL by concatenation, or hand-rolled crypto.

### 9. Test quality

- Tests fail for the right reason — re-read the red excerpt in `05-implementation-log.md`.
- **One behavior per test.** A test with three unrelated `Should*` blocks and three arrange
  sections is three tests.
- **Shouldly assertions only** (`result.Status.ShouldBe(...)`). No `Assert.Equal`, no FluentAssertions.
- **NSubstitute only. No Moq** — any `Moq` using directive or `new Mock<T>()` is `blocker`.
- **Narrowest scope that proves the behavior**: a plain unit test over a handler beats a
  `WebApplicationFactory` test; a `WebApplicationFactory` test beats a Testcontainers test.
  A container-backed test for logic that has no database in it is `major`.
- Integration tests use `Respawn` between tests for isolation, and Testcontainers for real
  dependencies — no shared mutable static state, no ordering dependence between tests.
- `Verify.XUnit` snapshots reviewed in the diff, not blindly accepted. A `.received.*` file
  committed is `blocker`.
- **No skipped tests** — `Skip = "..."` needs an issue reference and an ADR; `Skip` with no
  reason is `blocker`.
- **Deterministic time.** No `DateTime.Now`/`UtcNow` or `DateTimeOffset.UtcNow` in production
  code under test — inject `TimeProvider` and use `FakeTimeProvider` in tests. No `Thread.Sleep`
  or `Task.Delay` as a synchronisation mechanism.
- No removed or weakened assertion in the diff. Coverage holds; new code ≥ 0.95 line and branch.
- If `stryker-config.json` exists, survived mutants in changed files are all killed or
  ADR-waived. If it does not, the `mutation` gate reporting `skipped` is expected, not a finding.

### 10. Clarity over cleverness

Apply the `clarity-over-cleverness` skill.

- Names match the domain language in the `01-spec.md` glossary. No `data`, `temp`, `helper`,
  `mgr`, `obj`.
- **A literal appearing twice is extracted to a named constant.** Magic numbers and repeated
  strings are `minor` on first sight, `major` when they encode a business rule.
- No nested ternaries, no clever LINQ chain that needs a comment to parse, no expression-bodied
  member hiding three decisions.
- No dead code, no commented-out blocks, no `TODO` without an issue reference.
- Method length ≤ 30 lines (guideline). A handler doing four things is four methods.
- `var` where the type is obvious from the right-hand side; explicit type where it is not.
- No fully-qualified type names inline in code — use a `using` directive and the simple name.

### 11. Packaging and dependencies

- **No `Version=` attribute on any `PackageReference`.** Central package management is in force;
  every version lives in `Directory.Packages.props`. A version in a `.csproj` is `blocker`.
- Any new `PackageVersion` entry in `Directory.Packages.props` was confirmed with the user. An
  unannounced new dependency is `blocker`.
- `global.json` SDK pin unchanged, or changed with an ADR.
- No `<TargetFramework>` divergence between projects.
- No `<NoWarn>` or `<TreatWarningsAsErrors>false</TreatWarningsAsErrors>` added.
- New project added to the solution file and to the right harness gate.
- `dotnet list package --vulnerable` clean, or one ADR-backed waiver per advisory.

## Findings table format

```markdown
| ID | Severity | Section | File | Line | Finding | Suggested fix |
|----|---------|---------|------|------|---------|---------------|
| F-001 | blocker | security | src/Ordering/Features/Orders/CreateOrderEndpoint.cs | 34 | `.AllowAnonymous()` on a state-changing POST, no comment or ADR | apply `RequireAuthorization("orders:write")` |
| F-002 | major | data-access | src/Ordering/Features/Orders/GetOrdersHandler.cs | 21 | list query has no `Take` and no `AsNoTracking` | add page-size ceiling + `AsNoTracking()` |
| F-003 | minor | clarity | src/Ordering/Features/Orders/Pricing.cs | 58 | `0.10m` repeated in three places | extract `MemberDiscountRate` constant |
```

Every finding carries a `file:line`. A finding without one is not actionable and must not be
written.

## Reviewer conduct

The reviewer **reports; it does not repair.**

- Never commit, never stage, never push.
- Never edit production code, tests, configuration, or a migration. If a fix is obvious, put the
  fix in the *Suggested fix* column as text.
- Never re-run the harness to "see if it passes now" after suggesting a change.
- Never raise a finding without reading the file at the cited line.
- Every waiver requires an ADR under `.specs/<feature-id>/adr/`; a waiver recorded in
  `08-code-review.md` with no ADR is itself a blocker.

## Verdict

End `08-code-review.md` with one of:

- ✅ **Approve** — no blockers; minors noted; safe to commit.
- ⚠️ **Approve with waivers** — blockers/majors waived via the listed ADRs; commit OK.
- ❌ **Request changes** — blockers exist, no waivers; commit blocked.

## Forbidden (for the reviewer)

- Approving with an unwaived blocker, or downgrading a blocker to a minor to reach approval.
- Editing code, tests, or config instead of reporting a finding.
- A finding without a `file:line` reference.
- A waiver without an ADR.
- Committing, staging, pushing, or re-running the harness.
