---
name: dotnet-task-decomposition
description: Decompose a .NET 10 design into 1–4 hour TDD-shaped tasks with stable IDs, AC traceability, .NET-shaped files-in-scope (production AND test paths), and per-task harness gates. Use when authoring `04-tasks.md` or writing the `.tdd-state.json` the build hooks read.
when_to_use:
  - Phase 3 (Plan) — turning `03-design.md` + `01-spec.md` into an ordered task list.
  - Re-planning after a spec change, an added AC, or a rejected task.
  - Whenever `.claude/hooks/enforce-files-in-scope.sh` blocks an edit the task should have declared.
authoritative_references:
  - .claude/templates/tasks.template.md
  - .claude/checklists/implementation-dod.md
  - .claude/skills/dotnet-10-conventions/SKILL.md
  - .claude/skills/dotnet-testing-patterns/SKILL.md
  - https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/
---

# .NET Task Decomposition

## Sizing rule

Each task is **1–4 hours** for a competent engineer. If larger, split. If smaller than 30 minutes,
fold into the previous task.

## Shape

A good task has all of:

1. **Stable ID** `T-NNN`. Never renumber.
2. **Linked AC-IDs** — what user-visible criteria this task moves forward.
3. **Test-IDs** — at least one (`T-NNN-T1`, `T-NNN-T2`).
4. **Files in scope** — every path the task is allowed to edit, **production and tests**.
5. **Dependencies** — `T-NNN` IDs that must be `done` first.
6. **Gates** — which gates from `artifacts/harness-summary.json` run for this task.
7. **Rollback** — one sentence on how to revert if validation fails.

## Files in scope: the test path is mandatory

`files_in_scope` lists paths in .NET shapes:

```
src/Ordering/Features/Orders/CreateOrderEndpoint.cs
src/Ordering/Features/Orders/CreateOrderHandler.cs
tests/Ordering.Tests/Features/Orders/CreateOrderTests.cs
tests/Ordering.IntegrationTests/Features/Orders/CreateOrderEndpointTests.cs
```

**Every task must include the test path.** This is not a style preference.
`.claude/hooks/enforce-files-in-scope.sh` now blocks an edit to any file the active task has not
declared — **including test files**. A task whose `files_in_scope` names only production paths
cannot be built at all: the red step is the first thing `/build` does, and the hook rejects the
test file before a single line is written. The task is dead on arrival and must be re-planned.

So, for every task:

- At least one `tests/<Module>.Tests/...` path, or a `tests/<Module>.IntegrationTests/...` path
  when the task's proof is an integration test.
- Concrete paths, never globs. `tests/Ordering.Tests/**` will not satisfy the hook.
- The path as it will exist, including the mirrored `Features/<Slice>/` folder.
- A refactor-only task still declares the test files it will touch, even if it only re-runs them.

## Migrations are always their own task

EF Core migration work never rides along inside a feature task.

- One task per migration, ordered **before** any task that depends on the new schema.
- `files_in_scope` includes the generated migration files —
  `src/Ordering/Migrations/20260920120000_AddOrderLineTotals.cs`, its `.Designer.cs`, and
  `src/Ordering/Migrations/ShopDbContextModelSnapshot.cs` — plus the changed entity/configuration
  and the migration's own test.
- Gates: `unit` and `it` (the integration gate is what actually applies the migration against a
  Testcontainers Postgres).
- Rollback: `dotnet ef migrations remove`, plus reverting the entity change.
- Never `EnsureCreated()`. Never edit a migration that has already been released.

## Greenfield: the scaffold task comes first and carries no AC

The first task of a greenfield feature is the project/solution scaffold. It is the one task with
an empty `AC-IDs` list, because it delivers no user-visible behavior.

`files_in_scope` for the scaffold:

```
global.json
Directory.Build.props
Directory.Packages.props
Shop.sln
src/Ordering/Ordering.csproj
tests/Ordering.Tests/Ordering.Tests.csproj
tests/Ordering.IntegrationTests/Ordering.IntegrationTests.csproj
tests/Ordering.Tests/ScaffoldSmokeTests.cs
```

Its proof is a smoke test that the solution builds and the test host runs — so it still has a
test path, and its gate is `unit` only. Refactor-only tasks are the other exception to the
AC rule; **every other task maps to at least one AC.**

## Gates per task

Name the gates exactly as they appear in `artifacts/harness-summary.json`:

| Gate | Runs | Put it on a task when |
|---|---|---|
| `unit` | xUnit v3 tests without `[Trait("Category","Integration")]` | Always. Every task. |
| `it` | tests with `[Trait("Category","Integration")]` — `WebApplicationFactory`, Testcontainers, Respawn | The task adds a migration, an endpoint, or anything crossing a process boundary. |
| `coverage` | Cobertura at `artifacts/coverage/cobertura.xml`; 0.90 floor, 0.95 on new code | Every task that adds production code. |
| `mutation` | Stryker.NET, 0.75 kill rate — `skipped` unless `stryker-config.json` exists | Only when the repo has opted in. Otherwise omit it; a `skipped` gate is not a task concern. |

## Ordering rules

Dependencies flow inward. In order:

1. **Scaffold** (greenfield only).
2. **Contract and migration** — request/response DTOs, validators, domain types; EF Core
   migration as its own task.
3. **Handler** — the business logic, unit-tested in isolation with NSubstitute doubles.
4. **Endpoint** — the Minimal API mapping, `TypedResults`, the validation filter wiring.
5. **Integration test** — `WebApplicationFactory` + Testcontainers through the real endpoint.
6. **Cross-cutting** — ArchUnitNET rules, observability, ProblemDetails shape.

Contract and migration before handler. Handler before endpoint. Endpoint before integration test.
Two tasks never edit the same file in parallel — serialize them with a dependency.
**One task touches one slice.** A change spanning `Features/Orders` and `Features/Payments` is
two tasks plus a contract task, not one.

## Worked example — "Create order with line totals"

```markdown
### T-001 — Scaffold the Ordering module and test projects
- **AC-IDs:** none (scaffold)
- **Test-IDs:** T-001-T1 (solution builds; test host discovers zero-failure suite)
- **Files in scope:**
  - `global.json`
  - `Directory.Build.props`
  - `Directory.Packages.props`
  - `Shop.sln`
  - `src/Ordering/Ordering.csproj`
  - `tests/Ordering.Tests/Ordering.Tests.csproj`
  - `tests/Ordering.IntegrationTests/Ordering.IntegrationTests.csproj`
  - `tests/Ordering.Tests/ScaffoldSmokeTests.cs`
- **Dependencies:** none
- **Gates:** unit
- **Rollback:** delete the solution and both project folders.

### T-002 — `CreateOrderRequest` DTO + FluentValidation rules
- **AC-IDs:** AC-001, AC-004
- **Test-IDs:** T-002-T1 (rejects blank reference), T-002-T2 (rejects >100 lines), T-002-T3 (caps reference at 32 chars)
- **Files in scope:**
  - `src/Ordering/Features/Orders/CreateOrderRequest.cs`
  - `src/Ordering/Features/Orders/CreateOrderRequestValidator.cs`
  - `tests/Ordering.Tests/Features/Orders/CreateOrderRequestValidatorTests.cs`
- **Dependencies:** T-001
- **Gates:** unit, coverage
- **Rollback:** delete the three files; nothing references them yet.

### T-003 — EF Core migration `AddOrderLineTotals`
- **AC-IDs:** AC-002
- **Test-IDs:** T-003-T1 (migration applies to an empty Postgres container and the snapshot round-trips)
- **Files in scope:**
  - `src/Ordering/Domain/OrderLine.cs`
  - `src/Ordering/Persistence/OrderLineConfiguration.cs`
  - `src/Ordering/Migrations/20260920120000_AddOrderLineTotals.cs`
  - `src/Ordering/Migrations/20260920120000_AddOrderLineTotals.Designer.cs`
  - `src/Ordering/Migrations/ShopDbContextModelSnapshot.cs`
  - `tests/Ordering.IntegrationTests/Persistence/MigrationTests.cs`
- **Dependencies:** T-001
- **Gates:** unit, it
- **Rollback:** `dotnet ef migrations remove`, revert the entity and configuration.

### T-004 — `CreateOrderHandler` computes line totals and order total
- **AC-IDs:** AC-002, AC-003
- **Test-IDs:** T-004-T1 (sums line totals), T-004-T2 (applies member discount at the £100 boundary), T-004-T3 (returns NotFound result for an unknown customer)
- **Files in scope:**
  - `src/Ordering/Features/Orders/CreateOrderHandler.cs`
  - `src/Ordering/Features/Orders/CreateOrderResult.cs`
  - `tests/Ordering.Tests/Features/Orders/CreateOrderHandlerTests.cs`
- **Dependencies:** T-002, T-003
- **Gates:** unit, coverage
- **Rollback:** delete the handler and result type; T-005 is not yet written.

### T-005 — `POST /api/orders` endpoint with TypedResults and ProblemDetails
- **AC-IDs:** AC-001, AC-005
- **Test-IDs:** T-005-T1 (201 + Location on success), T-005-T2 (400 ProblemDetails on validation failure), T-005-T3 (404 ProblemDetails for unknown customer)
- **Files in scope:**
  - `src/Ordering/Features/Orders/CreateOrderEndpoint.cs`
  - `src/Ordering/OrderingEndpoints.cs`
  - `tests/Ordering.Tests/Features/Orders/CreateOrderEndpointTests.cs`
- **Dependencies:** T-004
- **Gates:** unit, coverage
- **Rollback:** remove the map call from `OrderingEndpoints`; delete the endpoint file.

### T-006 — Integration test: create order end to end
- **AC-IDs:** AC-001, AC-002, AC-003, AC-005
- **Test-IDs:** T-006-T1 (persists order and lines), T-006-T2 (409 on duplicate reference), T-006-T3 (401 when unauthenticated)
- **Files in scope:**
  - `tests/Ordering.IntegrationTests/Features/Orders/CreateOrderEndpointTests.cs`
  - `tests/Ordering.IntegrationTests/OrderingApiFixture.cs`
- **Dependencies:** T-005
- **Gates:** unit, it, coverage
- **Rollback:** delete both test files.

### T-007 — Authorization policy and rate limit for the orders group
- **AC-IDs:** AC-005
- **Test-IDs:** T-007-T1 (403 without `orders:write` scope), T-007-T2 (429 past the token-bucket limit)
- **Files in scope:**
  - `src/Ordering/OrderingEndpoints.cs`
  - `src/Ordering/OrderingModuleExtensions.cs`
  - `tests/Ordering.IntegrationTests/Features/Orders/CreateOrderAuthorizationTests.cs`
- **Dependencies:** T-006
- **Gates:** unit, it
- **Rollback:** revert the two `src/Ordering` files to the T-006 state.

### T-008 — ArchUnitNET rule: no cross-slice references in Ordering
- **AC-IDs:** none (cross-cutting refactor guard)
- **Test-IDs:** T-008-T1 (Features slices do not reference each other)
- **Files in scope:**
  - `tests/Ordering.Tests/Architecture/SliceBoundaryTests.cs`
- **Dependencies:** T-005
- **Gates:** unit
- **Rollback:** delete the test file.
```

## The `.tdd-state.json` contract

The architect writes this file at the end of `/plan`. The build hooks read it on every edit, so
its shape is a contract, not a convenience.

```json
{
  "active_task": null,
  "tasks": {
    "T-001": {
      "phase": "pending",
      "files_in_scope": [
        "global.json",
        "src/Ordering/Ordering.csproj",
        "tests/Ordering.Tests/ScaffoldSmokeTests.cs"
      ],
      "acs_covered": []
    },
    "T-002": {
      "phase": "pending",
      "files_in_scope": [
        "src/Ordering/Features/Orders/CreateOrderRequest.cs",
        "src/Ordering/Features/Orders/CreateOrderRequestValidator.cs",
        "tests/Ordering.Tests/Features/Orders/CreateOrderRequestValidatorTests.cs"
      ],
      "acs_covered": ["AC-001", "AC-004"]
    }
  }
}
```

Rules:

- `tasks` is a **map keyed by task id**, not an array. An array-shaped `tasks` field is
  **rejected by the hooks** — `enforce-files-in-scope.sh` and
  `block-impl-without-failing-test.sh` look up `.tasks["T-NNN"]` and will fail the edit outright
  rather than fall back. If `/build` reports "task not found" for a task that is plainly in
  `04-tasks.md`, this is why.
- `active_task` is `null` until `/build <task-id>` sets it, then holds exactly one task id.
- `phase` is one of `pending`, `red`, `green`, `refactor`, `simplify`, `done`.
- `files_in_scope` mirrors `04-tasks.md` **exactly** — repo-relative, forward slashes, no globs,
  no leading `./`. A path present in the markdown but missing here is an edit the hook will block.
- `acs_covered` is `[]` only for the scaffold task and refactor tasks.

## Anti-patterns

- "Implement ordering" — too big.
- A task with no `Test-IDs` — TDD impossible.
- A task whose `files_in_scope` is `src/**` or `tests/**` — the hook will block every edit.
- A task listing production files only — the red step cannot start.
- A migration folded into the endpoint task.
- Two tasks editing `OrderingEndpoints.cs` in parallel — serialize them.
- A task that says "refactor X" without an AC and without naming its test files — refactors
  happen inside the refactor phase of `/build`, not as an undeclared standalone task.
- Naming the gate `integration` or `integration-tests` instead of `it`.

## Self-check

- [ ] Every AC from `01-spec.md` is reachable from at least one task's `AC-IDs`.
- [ ] Every task has ≥1 `Test-ID`.
- [ ] **Every task's `files_in_scope` contains at least one `tests/...` path.**
- [ ] Every task fits 1–4 hours and touches exactly one slice.
- [ ] Every EF Core migration is its own task, ordered before its consumers.
- [ ] Gate names are drawn only from `unit`, `it`, `coverage`, `mutation`.
- [ ] `Files in scope` is concrete paths, not glob patterns.
- [ ] Dependency DAG has no cycles; contract → migration → handler → endpoint → integration.
- [ ] `.tdd-state.json` written with `tasks` as an object keyed by task id, `active_task: null`.
- [ ] `files_in_scope` in `.tdd-state.json` matches `04-tasks.md` path for path.

## Forbidden

- A task without a test path in `files_in_scope`.
- A task estimated at more than 4 hours.
- A task touching more than one slice.
- An AC in `01-spec.md` with no task covering it.
- A migration bundled into a feature task, or a migration task ordered after its consumer.
- Writing `.tdd-state.json` with `tasks` as an array.
- Glob patterns anywhere in `files_in_scope`.
