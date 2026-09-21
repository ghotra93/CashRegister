---
name: dotnet-coverage-and-mutation
description: Coverage and mutation policy for .NET 10 — Cobertura collection via Microsoft.Testing.Extensions.CodeCoverage with a 90% line+branch floor, 95% target, 95% on new code, plus opt-in Stryker.NET mutation testing at a 0.75 kill rate. Use when wiring the coverage or mutation gate, interpreting `artifacts/coverage/cobertura.xml`, or deciding whether a weak test is the real problem.
when_to_use:
  - Phase 5 (Test) — deciding whether a test suite is strong enough, not merely green.
  - Phase 6 (Validate) — the `coverage` and `mutation` gates in `artifacts/harness-summary.json`.
  - Brownfield onboarding — recording the existing baseline instead of failing the build on day one.
  - Code review — when a diff adds lines but not assertions.
authoritative_references:
  - https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-code-coverage
  - https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-intro
  - https://stryker-mutator.io/docs/stryker-net/introduction/
  - .claude/skills/dotnet-code-review-rubric/SKILL.md
  - .claude/checklists/validation-gates.md
---

# .NET Coverage and Mutation Policy

## How coverage is collected

Coverage comes from `Microsoft.Testing.Extensions.CodeCoverage`, the Microsoft.Testing.Platform
coverage extension. There is no `coverlet`, no `dotnet test --collect`, no VSTest data collector
in this stack. The test executable itself emits the report.

```bash
# what .github/scripts/harness-dotnet.sh runs for the coverage gate
dotnet run --project tests/Ordering.Tests/Ordering.Tests.csproj -- \
  --coverage \
  --coverage-output-format cobertura \
  --coverage-output "$PWD/artifacts/coverage/cobertura.xml"
```

The version of the extension package lives in `Directory.Packages.props` like every other
version. Never pin it in the `.csproj`.

The harness parses exactly one file — `artifacts/coverage/cobertura.xml` — and reads the
root element's `line-rate` and `branch-rate` attributes, then writes the `coverage` gate into
`artifacts/harness-summary.json`:

```json
{
  "coverage": {
    "status": "pass",
    "line_rate": 0.934,
    "branch_rate": 0.912,
    "report": "artifacts/coverage/cobertura.xml"
  }
}
```

New-code coverage is a separate script — `.github/scripts/check-new-code-coverage-dotnet.sh` —
which intersects `git diff --unified=0 origin/main...HEAD` line ranges with the Cobertura
`<line number="N" hits="0"/>` entries and writes `artifacts/new-code-coverage.json`.

## Policy table

| Metric | Hard floor (gate fails below) | Target (tracked, not enforced) | New / changed lines |
|---|---|---|---|
| Line coverage | 0.90 | 0.95 | 0.95 |
| Branch coverage | 0.90 | 0.95 | 0.95 |
| Mutation kill rate | n/a — gate `skipped` by default | 0.85 | 0.75 when enabled |

"New code" = lines added or modified versus `origin/main`. The floor is a floor, not a goal:
a repo sitting at exactly 0.90 is a repo with a testing problem, not a compliant repo.

### When a gate fails

| Gate result | Correct response |
|---|---|
| `coverage` fails on overall line/branch | Find the lowest-covered file in the diff and test it. Do not touch the threshold. |
| `coverage` fails only on new code | The task's own tests are thin. Add the missing cases before moving the task to `done`. |
| `coverage` fails on a file you did not touch | Pre-existing debt. Record it in `.specs/_baseline.json` and raise it in `07-validation-report.md`; do not fix it inside an unrelated feature task. |
| `mutation` fails | Read the survived mutants; each one names a missing assertion. Write the test that kills it. |
| Gate fails and you believe the threshold is wrong | Write an ADR. A threshold change without an ADR is a blocker. |

## Legitimate exclusions

`[ExcludeFromCodeCoverage]` is allowed on exactly three shapes:

1. **Generated code** — Mapster-generated mappers, EF Core migration classes, source-generated
   partials. Prefer the generator emitting the attribute itself.
2. **Composition-root bootstrap** — `Program.cs` top-level statements that only wire the
   container. Anything in `Program.cs` with an `if` is not bootstrap; move it into a testable
   extension method.
3. **DTO records with no logic** — a positional `record` with no body, no computed member,
   no validation.

```csharp
[ExcludeFromCodeCoverage(Justification = "EF Core generated migration; verified by the it gate")]
public partial class AddOrderLineTotals : Migration { /* generated */ }
```

**Every exclusion carries a written justification** — the attribute's `Justification` argument,
or a one-line comment directly above it naming who decided and why. An exclusion without a
justification is a review blocker, and an exclusion introduced to make a red gate green is a
threshold change in disguise.

Never exclude: handlers, validators, endpoint filters, options validation, anything under
`src/<Module>/Features/`, or a whole assembly.

## Why branch coverage catches what line coverage misses

Line coverage asks "did this line execute". Branch coverage asks "did every outcome of every
decision execute". A single test drives line coverage to 100% while leaving half the behavior
unexercised.

```csharp
public static decimal Discount(Order order) =>
    order.Total > 100m && order.Customer.IsMember ? order.Total * 0.1m : 0m;
```

One test with a £150 order from a member: **100% line coverage, 50% branch coverage.**
The `IsMember == false` path, the `Total <= 100` path, and the short-circuit interaction are
all untested. Branch coverage is what tells you the boundary at `100m` was never probed —
which is exactly the bug a mutation run would then confirm.

Because of this, the floor is 0.90 on **both** metrics, and a diff that moves line rate up
while moving branch rate down is a regression regardless of the headline number.

## Reading a Cobertura report by hand

```xml
<coverage line-rate="0.934" branch-rate="0.912" version="..." timestamp="...">
  <packages>
    <package name="Ordering" line-rate="0.951" branch-rate="0.928">
      <classes>
        <class name="Ordering.Features.Orders.CreateOrderHandler"
               filename="src/Ordering/Features/Orders/CreateOrderHandler.cs"
               line-rate="0.80" branch-rate="0.50">
          <lines>
            <line number="41" hits="3" branch="true" condition-coverage="50% (1/2)"/>
            <line number="47" hits="0" branch="false"/>
          </lines>
        </class>
      </classes>
    </package>
  </packages>
</coverage>
```

Read it in this order:

1. Root `line-rate` / `branch-rate` — the gate numbers.
2. Sort `<class>` elements by `line-rate` ascending — the worst file first.
3. Inside that class, `hits="0"` is dead-to-tests code; `condition-coverage="50% (1/2)"` is a
   half-tested decision, which is almost always the more interesting finding.

```bash
# quick worst-offenders scan
grep -o 'class name="[^"]*" [^>]*line-rate="[0-9.]*"' artifacts/coverage/cobertura.xml \
  | sort -t'"' -k6 -g | head -10
```

## Mutation testing: what coverage cannot prove

Coverage proves the code ran. **Mutation proves a test would have failed if the code were
wrong.** Stryker.NET rewrites your source in small ways — flips `>` to `>=`, swaps `&&` for
`||`, replaces a string with `""`, removes a statement — then reruns the suite per mutant.

| Outcome | Meaning | Action |
|---|---|---|
| **Killed** | A test failed under the mutation. | Good. Nothing to do. |
| **Survived** | Every test still passed with broken code. | A real gap. Write the assertion that would fail. |
| **No coverage** | No test executed the mutated line at all. | A coverage gap, not an assertion gap. Fix via the coverage gate first. |
| **Timeout** | Mutation caused a loop/hang. | Counted as killed. Ignore. |
| **Compile error** | Mutant does not build. | Excluded from the rate. Ignore. |

Kill rate = `killed / (killed + survived)`. **No-coverage mutants are the coverage gate's job,
not the mutation gate's** — do not congratulate yourself for a high kill rate on 40% of the code.

## Mutation is OFF by default on this stack

The `mutation` gate is **opt-in**. `.github/scripts/harness-dotnet.sh` looks for
`stryker-config.json` at the repo root; if it is absent, the gate emits:

```json
{ "mutation": { "status": "skipped", "reason": "no stryker-config.json" } }
```

A `skipped` mutation gate is a passing harness run. Turning mutation on means **adding
`stryker-config.json`** — that file's existence is the switch. Do it deliberately, with an ADR,
because it changes every future validation run's wall-clock time.

### Enabling it

```bash
dotnet new tool-manifest            # if .config/dotnet-tools.json does not exist
dotnet tool install dotnet-stryker  # version lands in .config/dotnet-tools.json
```

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

`"break": 75` is the 0.75 threshold the harness enforces. The `it` gate's Testcontainers
projects are deliberately **not** in `test-projects` — mutating against container-backed tests
turns minutes into hours.

### Keep runs in minutes, not hours

- `"since": { "target": "origin/main" }` — mutate only files the branch changed. This is the
  single biggest lever; a full-solution run is a nightly concern, never a per-task one.
- Scope `mutate` to `src/<Module>/Features/**` — the code with decisions in it.
- `"ignore-mutations": ["Logging"]` — nobody asserts on log strings, so those mutants only
  ever survive and only ever produce noise.
- Unit projects only. Never point Stryker at `tests/<Module>.IntegrationTests`.

## Survived-mutant patterns and what each one means

| Mutation | Survived because | The test you are missing |
|---|---|---|
| `>` → `>=` (boundary) | You tested 5 and 50, never the threshold itself. | Assert at exactly the boundary and one either side. |
| `&&` → `\|\|` | Only one operand was ever false. | A case where each operand is independently false. |
| `return x;` → `return default;` | The caller's result is never asserted, only its type or non-nullness. | `result.Value.ShouldBe(expected)`, not `result.ShouldNotBeNull()`. |
| String literal → `""` | The message/key is produced but never compared. | Assert the ProblemDetails `title`/`detail` text or the error code. |
| Statement removal (a `_repo.Add(...)` call) | The side effect is never verified. | `_repo.Received(1).Add(Arg.Is<Order>(o => o.Id == id))`. |
| `+` → `-` in a total | Arithmetic tested only with zeros or identical operands. | Distinct non-zero operands with a hand-computed expected value. |
| `if (guard) throw` removed | The failure path has no test. | A test asserting the thrown/returned failure for the invalid input. |

Each fix is a test carrying the same `[Trait("AC","AC-NNN")]` as the behavior it protects, and
gets logged in `06-test-plan.md`.

If a mutant is genuinely equivalent — a defensive guard unreachable from any public entry point —
document it in `08-code-review.md` as a waiver with an ADR. Do not widen `ignore-mutations` to
silence it.

## Forbidden

- Lowering `break`, the 0.90 floor, or the 0.95 new-code threshold to make a gate pass.
  Thresholds move up, by ADR, never down to meet the code.
- `[ExcludeFromCodeCoverage]` on code with real branches — a handler, a validator, an options
  class, anything with an `if`, `switch`, `?:`, `??`, or a `try`.
- `[ExcludeFromCodeCoverage]` without a written justification.
- Excluding a whole assembly, project, or `src/<Module>/Features/**` from coverage.
- Coverage theatre: a test that executes the code and asserts nothing — no `Should*` call, or
  only `ShouldNotBeNull()` on a value whose contents matter.
- Deleting, `Skip`-ing, or commenting out a failing test instead of fixing the code.
- Pointing Stryker at the Testcontainers integration projects.
- Claiming the mutation gate passed when it reported `"status": "skipped"`.
