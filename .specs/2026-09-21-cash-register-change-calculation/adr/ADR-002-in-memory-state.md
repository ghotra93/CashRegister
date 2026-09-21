# ADR-002: In-memory state for the divisor and uploaded files; no database in v1

- **Status:** accepted
- **Date:** 2026-09-21
- **Deciders:** user, `dotnet-architect`

## Context and problem statement

The divisor is changeable from the UI (Q-009), and processed files must be listed and downloadable (Q-010). The user said both may live in memory for v1 and be persisted in v2 (NQ-001, NQ-003). The default toolkit layer is EF Core with migrations.

## Decision drivers

- NFR-005 and the user's explicit v1 scope.
- Keeping a seam so that v2 persistence does not change callers.

## Considered options

1. EF Core + SQLite now.
2. In-memory singletons behind interfaces (`IDivisorSettings`, `IUploadedFileStore`).

## Decision outcome

Chosen option: **Option 2**. The EF Core / migrations layer is **deferred** to v2.

### Consequences

- Positive: no schema or migration tasks, and fast tests.
- Negative: state is lost on restart. Only a single API instance is supported. The file store has no retention limit, which is bounded in practice by the request size and the line limit. v2 swaps in DB-backed implementations of the same interfaces.

## Links

- Source: `01-spec.md` NFR-005, NQ-001, NQ-003
