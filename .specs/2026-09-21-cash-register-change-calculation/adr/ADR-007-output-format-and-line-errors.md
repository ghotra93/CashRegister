# ADR-007: Output file format and line error wording

- **Status:** accepted
- **Date:** 2026-09-21
- **Deciders:** user, `dotnet-architect`

## Context and problem statement

The spec says invalid lines "emit the error message on that output line" (Q-005) but does not fix the wording. The line terminator and download file name are also unspecified.

## Decision outcome

- Output lines are joined with `\n` (LF), with no trailing newline. It is UTF-8 without a BOM, `text/plain; charset=utf-8`.
- The download file name is `<original name without extension>-change.txt`.
- Every line error starts with `Error: ` so the cashier and any tooling can spot it:
  - `Error: invalid line, expected '<owed>,<paid>'`
  - `Error: amounts must not be negative`
  - `Error: amount paid is less than amount owed`
  - `Error: amounts must have at most 2 decimal places` (the digit count comes from the currency)
- When no change is due, the line is `No change` (AC-016).
- Checks run in this order: format, then negative, then decimals, then paid < owed. The first failing check wins.

### Consequences

- The user may change the wording at design review. The tests assert these exact strings.

## Links

- Source: `01-spec.md` AC-016 to AC-020, Q-004, Q-005
