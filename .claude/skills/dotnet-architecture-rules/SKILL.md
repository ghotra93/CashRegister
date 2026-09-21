---
name: dotnet-architecture-rules
description: ArchUnitNET rules that encode the modular-monolith vertical-slice boundaries as executable tests — cross-module access only through `Contracts`, no slice-to-slice references, persistence types kept out of the API surface, naming conventions, and no ambient time. Use when defining, reviewing, or ratcheting architecture invariants in a .NET solution.
when_to_use:
  - Phase 3 (Plan) — stating the boundary a new module or slice must respect.
  - Phase 5 (Test) — adding or extending the architecture suite.
  - Brownfield onboarding — freezing the current violation count instead of failing the build on day one.
  - Phase 7 (Code review) — when a diff adds a project reference or reaches across a slice.
authoritative_references:
  - https://archunitnet.readthedocs.io/en/latest/
  - https://github.com/TNG/ArchUnitNET
  - https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures
---

# .NET Architecture Rules (ArchUnitNET)

> The boundaries in `dotnet-10-conventions` are only real if a test fails when they are crossed. This skill is that test suite.

## The Layout Being Enforced

```
src/
├── Api/                                 # host: composition only
├── Ordering/                            # module
│   ├── Contracts/                       # the ONLY public surface
│   ├── Features/<Slice>/                # endpoint + handler + DTOs + validator
│   ├── Domain/                          # entities, value objects, invariants
│   └── Persistence/                     # DbContext + Configurations
├── GiftCards/                           # module, same shape
└── Shared/                              # cross-cutting primitives only
tests/
└── Ordering.Tests/Architecture/         # this suite
```

Invariants, in priority order:

| Invariant | Why it matters |
|---|---|
| Cross-module access only via `Contracts` | Lets a module be extracted into a service later |
| No slice-to-slice references | A slice is the unit of change; coupling slices destroys that |
| `Persistence`/`Domain` types never reach the API layer or a response DTO | Stops the schema leaking into the wire contract |
| `Domain` depends on nothing framework-shaped | Keeps invariants testable without a host |
| Names match roles (`*Endpoint`, `*Handler`, `*Validator`, `*Configuration`) | Makes the layout greppable and the slice template obvious |
| Time comes from `TimeProvider` | Determinism in tests, correctness across timezones |

## One Fixture, Loaded Once

Loading assemblies is expensive. Load them once into a `static readonly` field and share it across every rule class.

```csharp
namespace Ordering.Tests.Architecture;

internal static class Architectures
{
    internal static readonly ArchUnitNET.Domain.Architecture Solution =
        new ArchLoader()
            .LoadAssemblies(
                typeof(Ordering.OrderingModule).Assembly,
                typeof(GiftCards.GiftCardsModule).Assembly,
                typeof(Shared.Result).Assembly,
                typeof(Program).Assembly)
            .Build();

    internal const string OrderingNs = "Ordering";
    internal const string GiftCardsNs = "GiftCards";
}
```

Rules are `static readonly` fields too, so a typo in a predicate fails at class-init rather than per test.

```csharp
public sealed class ModuleBoundaryRules
{
    private static readonly IObjectProvider<IType> OrderingInternals = Types()
        .That().ResideInNamespaceMatching(@"^Ordering\.(Features|Domain|Persistence)")
        .As("Ordering internals");

    private static readonly IObjectProvider<IType> GiftCardsTypes = Types()
        .That().ResideInNamespaceMatching(@"^GiftCards\.")
        .As("GiftCards types");

    [Fact]
    [Trait("AC", "AC-000")]
    public void GiftCards_DoesNotReachIntoOrderingInternals()
    {
        Types().That().Are(GiftCardsTypes).Should()
            .NotDependOnAny(OrderingInternals)
            .Because("cross-module access goes through Ordering.Contracts only")
            .Check(Architectures.Solution);
    }
}
```

`AC-000` is the reserved AC for architecture invariants; every architecture test carries it so the traceability script sees the suite.

## Rule: No Slice Sees Another Slice

```csharp
[Fact]
[Trait("AC", "AC-000")]
public void NoSliceDependsOnAnotherSlice()
{
    var sliceNamespaces = Architectures.Solution.Types
        .Select(t => t.Namespace.FullName)
        .Where(ns => Regex.IsMatch(ns, @"^\w+\.Features\.\w+$"))
        .Distinct();

    foreach (var slice in sliceNamespaces)
    {
        Types().That().ResideInNamespace(slice).Should()
            .NotDependOnAny(Types().That()
                .ResideInNamespaceMatching(@"^\w+\.Features\.\w+$")
                .And().DoNotResideInNamespace(slice))
            .Because($"{slice} must not reference another slice; promote shared logic to the module root")
            .Check(Architectures.Solution);
    }
}
```

The fix for a violation is never a new reference. It is promoting the shared type to the module root or to `Contracts`.

## Rule: Persistence And Domain Stay Out Of The Wire

```csharp
[Fact]
[Trait("AC", "AC-000")]
public void EndpointsDoNotDependOnPersistence()
{
    Classes().That().HaveNameEndingWith("Endpoint").Should()
        .NotDependOnAny(Types().That().ResideInNamespaceMatching(@"\.Persistence"))
        .AndShould()
        .NotDependOnAny(Types().That().ResideInNamespaceMatching(@"\.Domain"))
        .Because("an endpoint binds DTOs and calls a handler; it never touches EF Core or an entity")
        .Check(Architectures.Solution);
}

[Fact]
[Trait("AC", "AC-000")]
public void ResponseDtosDoNotExposeEntitiesOrDbContext()
{
    Classes().That().HaveNameEndingWith("Response").Should()
        .NotDependOnAny(Classes().That().AreAssignableTo(typeof(DbContext)))
        .AndShould()
        .NotDependOnAny(Types().That().ResideInNamespaceMatching(@"\.Domain"))
        .Because("a response DTO is the wire contract; an entity in it couples the schema to the API")
        .Check(Architectures.Solution);
}

[Fact]
[Trait("AC", "AC-000")]
public void OnlyPersistenceDependsOnEfCore()
{
    Types().That().DoNotResideInNamespaceMatching(@"\.Persistence").Should()
        .NotDependOnAny(Types().That().ResideInNamespace("Microsoft.EntityFrameworkCore", true))
        .Because("EF Core is a persistence detail")
        .Check(Architectures.Solution);
}
```

The handler is the one place allowed to hold a `DbContext`; if a handler grows a second responsibility, extract a repository rather than relaxing the rule.

## Rule: Domain Is Framework-Free

```csharp
[Fact]
[Trait("AC", "AC-000")]
public void DomainDependsOnNoFramework()
{
    Types().That().ResideInNamespaceMatching(@"\.Domain").Should()
        .NotDependOnAny(Types().That().ResideInNamespace("Microsoft.AspNetCore", true))
        .AndShould()
        .NotDependOnAny(Types().That().ResideInNamespace("Microsoft.EntityFrameworkCore", true))
        .AndShould()
        .NotDependOnAny(Types().That().ResideInNamespace("FluentValidation", true))
        .Because("domain invariants must be testable without a host or a database")
        .Check(Architectures.Solution);
}
```

## Rule: Naming

```csharp
[Fact]
[Trait("AC", "AC-000")]
public void NamingConventionsHold()
{
    Classes().That().AreAssignableTo(typeof(IValidator)).Should()
        .HaveNameEndingWith("Validator").Check(Architectures.Solution);

    Interfaces().That().ResideInNamespaceMatching(@"\.Contracts").Should()
        .HaveNameStartingWith("I").Check(Architectures.Solution);

    Classes().That().ImplementInterface(typeof(IEntityTypeConfiguration<>)).Should()
        .HaveNameEndingWith("Configuration").Check(Architectures.Solution);

    Classes().That().HaveNameEndingWith("Handler").Should()
        .ResideInNamespaceMatching(@"\.Features\.\w+$")
        .AndShould().BeSealed()
        .Check(Architectures.Solution);

    Classes().That().HaveNameEndingWith("Endpoint").Should()
        .ResideInNamespaceMatching(@"\.Features\.\w+$")
        .AndShould().BeStatic()
        .Check(Architectures.Solution);
}
```

Roles and names: `<Slice>Endpoint` (static) and `<Slice>Handler` (sealed) in `Features/<Slice>/`, `<Request>Validator` beside them, `<Entity>Configuration` in `Persistence/Configurations/`, `I<Name>` in `Contracts/`.

## Rule: No Ambient Time

```csharp
[Fact]
[Trait("AC", "AC-000")]
public void NoAmbientDateTime()
{
    var offenders = Architectures.Solution.Types
        .Where(t => !t.NameContains("TimeProvider"))
        .SelectMany(t => t.GetMethodMembers())
        .Where(m => m.GetCalledMethods().Any(c =>
            c.FullName.Contains("System.DateTime::get_Now") ||
            c.FullName.Contains("System.DateTime::get_UtcNow") ||
            c.FullName.Contains("System.DateTimeOffset::get_UtcNow")))
        .Select(m => m.FullName)
        .ToList();

    offenders.ShouldBeEmpty(
        "time must come from an injected TimeProvider; found: " + string.Join(", ", offenders));
}
```

This is the one rule expressed as a LINQ query rather than a fluent ArchUnitNET assertion, because the violation is a **call site**, not a type dependency.

## Run In The Unit Gate

Architecture tests live in `tests/Ordering.Tests/Architecture/` and carry **no** `[Trait("Category", "Integration")]`. They therefore run inside the `unit` gate: same `dotnet test --filter "Category!=Integration"` invocation, same failure surface.

Do not create a separate `architecture` gate: a boundary violation is a compile-time-shaped mistake and belongs in the fastest gate, a separate gate is one more thing to forget, `harness-summary.json` would gain a key the JVM harness does not have, and the suite runs in seconds anyway.

## Ratcheting On A Brownfield Codebase

Failing the build on day one guarantees the rules get deleted. Freeze instead.

```csharp
// Frozen 2026-09-20 at HEAD. These numbers may only go DOWN.
private const int EndpointsTouchingPersistence = 12;

[Fact]
[Trait("AC", "AC-000")]
public void EndpointsDoNotDependOnPersistence_DoesNotRegress()
{
    var rule = Classes().That().HaveNameEndingWith("Endpoint").Should()
        .NotDependOnAny(Types().That().ResideInNamespaceMatching(@"\.Persistence"));

    var violations = rule.Evaluate(Architectures.Solution).Count(r => !r.Passed);

    violations.ShouldBeLessThanOrEqualTo(EndpointsTouchingPersistence,
        "architecture debt may not grow; fix an existing violation before adding one");
}
```

The procedure:

1. Run every rule in report mode, record the violation count per rule into `.specs/_baseline.json` alongside the harness gates.
2. Convert each failing rule into a ratchet test asserting `count <= frozen`.
3. **Lower the frozen number in the same PR that fixes a violation.** Never raise it.
4. When a count reaches 0, replace the ratchet with the strict rule and delete the entry.
5. A rule that passes today is added strict, never ratcheted.

Record the freeze date, the SHA, and the intended burn-down owner in an ADR. A ratchet with no ADR is permanent debt with extra steps.

## Forbidden

- `InternalsVisibleTo` used to let one module (or the host) reach another module's `internal` types. It is for test assemblies only.
- A module referencing another module's `DbContext`, `Domain` type, or `Features/` namespace.
- Reflection, `dynamic`, or a `[ModuleInitializer]` used to reach across a boundary a rule guards.
- `[Fact(Skip = "...")]` on an architecture test. A rule that cannot pass is either wrong (delete it, with an ADR) or ratcheted (freeze it) — never skipped.
- Raising a frozen violation count, or re-freezing at a higher number after a regression.
- A project reference that creates a cycle between modules — `Ordering` → `GiftCards.Contracts` is fine, a reference back is not.
- Deleting or weakening a rule to make a diff pass instead of fixing the diff.
- An architecture test without `[Trait("AC", "AC-000")]`, or moved into its own gate.
