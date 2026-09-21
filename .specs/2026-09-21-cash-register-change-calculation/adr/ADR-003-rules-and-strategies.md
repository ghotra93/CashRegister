# ADR-003: Change rules + strategies pipeline, integer minor units, random algorithm

- **Status:** accepted
- **Date:** 2026-09-21
- **Deciders:** user, `dotnet-architect`

## Context and problem statement

The client wants a configurable random divisor, more special cases later, and a lowest-priority-wins resolution (Q-008). Adding a rule must not change existing code (DC-001). The money arithmetic must be exact (AC-005).

## Decision drivers

- DC-001 (open/closed), AC-024, AC-005.
- Deterministic tests for random behaviour.

## Considered options

1. An `if/else` inside one calculator.
2. `IChangeRule { int Priority; Type StrategyType; bool Matches(Transaction) }` together with `IChangeStrategy` implementations. Both are registered in DI; `ChangeCalculator` picks the lowest-priority matching rule, or the minimal strategy when none matches.
3. A rules engine library.

## Decision outcome

Chosen option: **Option 2**.

- Amounts are held as `long` minor units (cents); `decimal` is used only while parsing. There is no floating point.
- If two rules have the same priority, registration order breaks the tie. A startup check rejects a rule whose strategy is not registered.
- The random strategy walks the denominations from largest to smallest. For each denomination except the smallest it takes `Random.NextInt64(0, remaining / value + 1)`; the smallest unit takes the remainder. The total is therefore always exact. The result may coincide with the minimal result, which is allowed (Q-007). `Random` is injected (`Random.Shared` in production, a seeded instance in tests).

### Consequences

- Positive: a new special case is one new sealed class plus one DI registration.
- Negative: the random distribution is biased toward small coins at the end. This is acceptable because the spec only requires the total to be correct.

## Links

- Source: `01-spec.md` AC-003, AC-004, AC-005, AC-013, AC-024, DC-001, Q-007, Q-008
