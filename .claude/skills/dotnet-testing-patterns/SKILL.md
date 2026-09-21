---
name: dotnet-testing-patterns
description: xUnit v3 on Microsoft.Testing.Platform — scope selection, NSubstitute doubles, Shouldly assertions, Verify snapshots, and the mandatory `[Trait("AC", …)]` traceability tag. Use when writing or reviewing any .NET test, especially when choosing between unit, slice, and integration scope.
when_to_use:
  - Phase 4 (red step of `/build`) — writing the failing test before any production code.
  - Phase 5 (Test) — adding cross-cutting suites and filling the test plan.
  - Phase 6 (Validate) — when the traceability matrix reports an uncovered AC or an orphaned test.
  - Anywhere a `WebApplicationFactory` test could be replaced with a plain unit test.
authoritative_references:
  - https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-intro
  - https://xunit.net/docs/getting-started/v3/microsoft-testing-platform
  - https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests
  - https://github.com/nsubstitute/NSubstitute
---

# .NET Testing Patterns

> xUnit v3 running on Microsoft.Testing.Platform. NSubstitute for doubles, Shouldly for assertions, Verify for snapshots, Testcontainers for real infrastructure.

## The One Rule That Cannot Be Broken

**Every test method carries `[Trait("AC", "AC-NNN")]` naming the acceptance criterion it proves.**

`.github/scripts/traceability-dotnet.sh` greps for exactly that attribute shape. An untagged test is **invisible to the traceability matrix** — it does not count toward its AC, the AC reports as uncovered, and `/validate` fails the feature even though the suite is green.

```csharp
[Fact]
[Trait("AC", "AC-007")]
public async Task HandleAsync_WhenGiftCardExpired_ReturnsRejected() { }
```

- The trait name is `AC`, capitalised, and the value is `AC-` plus three digits. No other spelling is recognised.
- A test that genuinely proves no AC (a regression guard, a config smoke test) still needs a trait — use the AC of the behaviour it protects, or add an AC to the spec.
- Multiple ACs → repeat the attribute. Do not comma-join values.

```csharp
[Fact]
[Trait("AC", "AC-007")]
[Trait("AC", "AC-008")]
public async Task HandleAsync_WhenGiftCardExpired_LogsAndReturnsRejected() { }
```

## Choose The Narrowest Scope That Can Fail For The Right Reason

| What the AC is about | Scope | Mechanism |
|---|---|---|
| Pure logic — pricing, a validator rule, a mapper | Unit | Plain xUnit, no host, no container |
| Handler behaviour with collaborators stubbed | Unit | NSubstitute doubles for every dependency |
| Route, status code, ProblemDetails shape, auth, endpoint filter | Slice | `WebApplicationFactory<Program>` with real services replaced |
| EF Core query, migration, constraint, concurrency token | Integration | Testcontainers `PostgreSqlContainer` |
| Endpoint → handler → real database, end to end | Integration | `WebApplicationFactory` + Testcontainers |
| Generated OpenAPI document shape | Unit | `WebApplicationFactory` + `Verify` snapshot |

**Prefer the narrowest scope that can fail for the right reason.** A validation rule tested through a container proves the rule *and* the network *and* the schema — when it goes red you learn nothing. Escalate scope only when the narrower scope cannot observe the behaviour.

- **Integration testing**: `WebApplicationFactory` customisation, Testcontainers lifetime and sharing, Respawn resets, and the xUnit v3 / MTP entrypoint config. Read [integration-testing.md](references/integration-testing.md)

## Naming And Structure

Test method names are `Method_Scenario_ExpectedOutcome`:

```
HandleAsync_WhenCustomerUnknown_ReturnsNotFound
Validate_WhenLineCountExceeds100_FailsWithLineLimitMessage
Total_WithZeroLines_ReturnsZero
```

- One behaviour per test. If the name needs "And", split the test.
- Arrange / Act / Assert, in that order, separated by one blank line each. No assertion in the arrange block.
- Test class name mirrors the subject: `PlaceOrderHandlerTests`, `PlaceOrderValidatorTests`, `PlaceOrderEndpointTests`.
- Test projects: `tests/Ordering.Tests` (unit + slice) and `tests/Ordering.IntegrationTests` (containers).

## Doubles — NSubstitute Only

```csharp
[Fact]
[Trait("AC", "AC-011")]
public async Task HandleAsync_WhenPricingSucceeds_PersistsOrderWithTotal()
{
    var pricing = Substitute.For<IOrderPricing>();
    pricing.Total(Arg.Any<IReadOnlyList<OrderLine>>()).Returns(42.50m);
    var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    var handler = new PlaceOrderHandler(db, pricing, timeProvider);

    var result = await handler.HandleAsync(Requests.Valid(), TestContext.Current.CancellationToken);

    result.Value.TotalAmount.ShouldBe(42.50m);
    pricing.Received(1).Total(Arg.Is<IReadOnlyList<OrderLine>>(l => l.Count == 1));
}
```

- `Substitute.For<T>()` creates the double; `Returns(...)` stubs; `Received(n)` / `DidNotReceive()` verifies.
- Verify an interaction only when the interaction **is** the behaviour (a message published, an audit row written). Otherwise assert on state.
- Never substitute the system under test. Never substitute a concrete type you own — extract an interface or use the real thing.
- `Moq` is banned outright and must not appear in `Directory.Packages.props`.

## Assertions — Shouldly Only

```csharp
result.Status.ShouldBe(OrderStatus.Placed);
result.Lines.ShouldNotBeEmpty();
result.Total.ShouldBeGreaterThan(0m);
problem.Errors.Keys.ShouldContain("Lines");

var ex = Should.Throw<DomainException>(() => Order.Create(customerId, -1m, now));
ex.Rule.ShouldBe("order.total-must-be-positive");

await Should.ThrowAsync<OperationCanceledException>(
    () => handler.HandleAsync(request, cancelledToken));
```

- `ShouldBe`, `ShouldNotBeNull`, `ShouldContain`, `Should.Throw<T>`, `Should.ThrowAsync<T>`.
- `Assert.*` is banned — Shouldly's failure messages carry the expression text, `Assert.Equal`'s do not.
- `ShouldNotBeNull()` alone is not an assertion about behaviour. Assert the value that matters.

## Table-Driven Cases

```csharp
[Theory]
[Trait("AC", "AC-004")]
[InlineData(0, false)]
[InlineData(1, true)]
[InlineData(999, true)]
[InlineData(1000, false)]
public void Validate_QuantityBoundaries_MatchesExpectedValidity(int quantity, bool expectedValid)
{
    var result = new PlaceOrderValidator().Validate(Requests.WithQuantity(quantity));

    result.IsValid.ShouldBe(expectedValid);
}
```

```csharp
public static TheoryData<string, int> RejectedSkus() => new()
{
    { "", StatusCodes.Status400BadRequest },
    { "ab", StatusCodes.Status400BadRequest },
    { "lowercase-sku", StatusCodes.Status400BadRequest },
};

[Theory]
[Trait("AC", "AC-005")]
[MemberData(nameof(RejectedSkus))]
public async Task Post_WithInvalidSku_ReturnsBadRequest(string sku, int expectedStatus) { }
```

- `InlineData` for literals; `MemberData` with `TheoryData<...>` when the case needs a non-constant. Never `object[]` arrays.
- The trait goes on the method once, not per `InlineData`.
- Always include the boundary and one value either side of it.

## Snapshots — Verify.XUnit

Use Verify where the *shape* is the contract and hand-written assertions would be long and brittle.

```csharp
[Fact]
[Trait("AC", "AC-009")]
public async Task Post_WithEmptyLines_ReturnsApprovedProblemDetailsShape()
{
    using var client = factory.CreateClient();

    var response = await client.PostAsJsonAsync("/api/v1/orders", Requests.WithNoLines());

    await Verify(await response.Content.ReadAsStringAsync())
        .ScrubMember("traceId")
        .ScrubMember("instance");
}
```

```csharp
[Fact]
[Trait("AC", "AC-001")]
public async Task OpenApiDocument_MatchesApprovedContract()
{
    using var client = factory.CreateClient();

    await Verify(await client.GetStringAsync("/openapi/v1.json"));
}
```

- Approve the `.verified.txt` file deliberately and commit it. A diff in that file during review is a contract change.
- Always scrub non-deterministic members (`traceId`, `instance`, timestamps, generated ids).
- Never snapshot a whole domain object as a substitute for asserting the one field the AC is about.

## The Integration Category Split

`.github/scripts/harness-dotnet.sh` runs two gates off one filter:

```bash
dotnet test --filter "Category!=Integration"   # -> gate "unit"
dotnet test --filter "Category=Integration"    # -> gate "it"
```

So every container-backed or network-backed test carries:

```csharp
[Trait("Category", "Integration")]
public sealed class OrderRepositoryTests : IClassFixture<PostgresFixture> { }
```

Put `[Trait("Category", "Integration")]` on the **class**. Omit it and the test runs inside the unit gate, where there is no container — it fails, and it drags the fast gate's runtime up.

## Determinism

- Time comes from an injected `TimeProvider`. Tests use `Microsoft.Extensions.TimeProvider.Testing.FakeTimeProvider` and advance it explicitly with `Advance(TimeSpan)`.
- No shared mutable state between tests. No `static` fields holding fixtures, counters, or caches. xUnit runs classes in parallel — shared state is a flaky test waiting to happen.
- Every test constructs its own data. Use a static factory class (`Requests.Valid()`, `Orders.Placed()`) returning fresh instances, never a shared instance.
- Cancellation tokens come from `TestContext.Current.CancellationToken`, so a hung test is cancelled rather than timing the run out.
- Tests must pass in any order and in isolation. Verify with a single-test run before committing.

## Forbidden

- **A test method without `[Trait("AC", "AC-NNN")]`** — invisible to `.github/scripts/traceability-dotnet.sh`, and a review blocker.
- `Moq` — the package, the `Mock<T>` type, `It.IsAny<T>()`, `Setup`, `Verify`. Use NSubstitute.
- `Assert.*` in any form (`Assert.Equal`, `Assert.True`, `Assert.Throws`). Use Shouldly.
- `Thread.Sleep` for synchronisation. Wait on the actual signal, or advance a `FakeTimeProvider`.
- `DateTime.Now` / `DateTime.UtcNow` — in tests **or** production. Inject `TimeProvider`.
- Tests that depend on execution order, or on data another test left behind.
- `[Fact(Skip = "...")]` or `[Theory(Skip = "...")]` left in the tree — delete the test or fix it; a skip needs an ADR and a ticket in the reason string.
- Real network or real database access in a unit-gate test.
- `.Result`, `.Wait()`, `GetAwaiter().GetResult()` in a test — make the test `async Task`.
- `object[]`-based `MemberData`; `static` mutable fixtures; a test asserting nothing.
