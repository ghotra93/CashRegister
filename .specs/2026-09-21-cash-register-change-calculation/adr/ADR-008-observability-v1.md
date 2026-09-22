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

## Amendment 2026-09-22: `/net-ship` instrumentation waiver

- `/net-ship` requires an OpenTelemetry `ActivitySource` span and a `Meter` for every new endpoint. **The user waived that rule for v1** ("Waive, keep OTel in v2", 2026-09-22), consistent with NQ-005.
- The five v1 endpoints (`POST/GET /api/files`, `GET /api/files/{id}/output`, `GET/PUT /api/settings/divisor`) are observable through the structured log events above (with counts and elapsed ms on `FileProcessed`) and through `/health`.
- The user also chose **no alerts for v1** ("No alerts (demo)"): there is no on-call, so detection is manual (user or reviewer report). The ship plan records this reason on every observability row.
- Revisit together with OpenTelemetry in v2: spans per upload, and a `Meter` for files, lines, error lines and divisor changes.

## Links

- Source: `01-spec.md` NFR-002, NFR-003, NQ-005
