# ADR-004: Currency registry; v1 processes files in USD

- **Status:** accepted
- **Date:** 2026-09-21
- **Deciders:** user, `dotnet-architect`

## Context and problem statement

Only USD is supported in v1, but adding a currency must follow the open/closed principle (Q-002, DC-002). The separator is tied to the currency (Q-013). The spec does not say how a file chooses its currency.

## Considered options

1. A currency field on the upload request.
2. An active currency from configuration (`CashRegister:Currency`, value `USD`), resolved through `ICurrencyRegistry`.

## Decision outcome

Chosen option: **Option 2**. v1 has no UI choice to make, and adding a request field would be inventing scope.

- `Currency` is an abstract class with `Code`, `DecimalSeparator`, `MinorUnitDigits` and `Denominations`. `UsdCurrency` is a subclass. The registry collects every `Currency` registered in DI.
- An unknown configured code fails at startup.
- The field separator is `,` for v1. If a future currency uses `,` as its decimal separator, the field separator becomes a per-currency property in that change.

### Consequences

- Positive: adding EUR means one class plus one registration.
- Negative: choosing the currency per file or per client needs a v2 spec.

## Links

- Source: `01-spec.md` DC-002, AC-029, Q-002, Q-013
