# ADR-008: Observability v1 — structured logs + health check; OpenTelemetry deferred

- **Status:** accepted
- **Date:** 2026-09-21
- **Deciders:** user, `dotnet-architect`

## Context and problem statement

The user wants structured logs and a health check in v1, with OpenTelemetry in v2 (Q-015, NQ-005). The toolkit default adds `ActivitySource` and `Meter` instrumentation.

## Decision outcome

- LoggerMessage source-generated events: `FileProcessed`, `FileRejected`, `DivisorChanged`. File contents are never logged.
- JSON console formatter outside Development.
- `/health` is a liveness endpoint (there are no dependencies to probe).
- **Deferred:** `ActivitySource`, `Meter` and the OpenTelemetry exporters, to v2. This overrides the toolkit default, as the user explicitly deferred it.

## Links

- Source: `01-spec.md` NFR-002, NFR-003, NQ-005
