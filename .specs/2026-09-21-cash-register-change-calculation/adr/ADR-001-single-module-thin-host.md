# ADR-001: Single `CashRegister` module + thin API host

- **Status:** accepted
- **Date:** 2026-09-21
- **Deciders:** user, `dotnet-architect`
- **Consulted:** —
- **Informed:** —

## Context and problem statement

The user chose "module with `Change` and `Currencies` as slices/folders" (NQ-004, DC-003). An earlier, unapproved first pass had already scaffolded a layered solution (`CashRegister.Domain`, `CashRegister.Application`, `CashRegister.Api` and three test projects). That layered shape conflicts with DC-003.

## Decision drivers

- DC-003: one module, with slices as folders.
- A vertical-slice convention (`dotnet-10-conventions`): a slice owns its types, endpoints and tests.
- The test-project layout that the hooks expect: `tests/<Module>.Tests`, `tests/<Module>.IntegrationTests`.

## Considered options

1. Keep the layered Domain/Application/Api projects.
2. One `CashRegister` class library (the module, with its endpoint mappings) plus a thin `CashRegister.Api` host.
3. Everything inside the Web project.

## Decision outcome

Chosen option: **Option 2**. It matches DC-003, and it keeps the host free of logic, so a v2 host (for example a worker or CLI) can reuse the module.

### Consequences

- Positive: slices stay cohesive, and ArchUnitNET can enforce the slice boundaries.
- Negative / trade-offs: T-001 deletes the first-pass projects. The module takes a `FrameworkReference` to `Microsoft.AspNetCore.App` in order to map its endpoints.

## Links

- Source: `01-spec.md` DC-003, NQ-004
