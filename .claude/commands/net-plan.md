---
description: Run /net-plan — see .claude/commands/net-plan.md for the authoritative spec.
argument-hint: see .claude/commands/net-plan.md
agent: dotnet-architect
---
# /net-plan

**Phase:** 3 — plan
**Owning agent:** `.claude/agents/dotnet-architect.md`
**Skills used:** `epic-slicing-planning`, `dotnet-task-decomposition`, `dotnet-10-conventions`, `openapi-contract-first`, `efcore-10-data-access`, `dotnet-architecture-rules`, `dotnet-security-baseline`, `dotnet-observability-ops`, `adr-authoring`

## Purpose
Translate a `PASS`-verdict spec into planning artifacts:

- Non-Epic mode: `03-design.md` + `04-tasks.md`
- Epic mode: `03-epic-design.md` + `03a-epic-roadmap.md`, then slice-level `03-design.md` + `04-tasks.md`

Then initialize `.tdd-state.json`.

## Inputs
- `<feature-id>`. Required.
- `--epic` (optional; forces Epic mode).

## Reads
- `.specs/<feature-id>/01-spec.md`, `.specs/<feature-id>/02-spec-review.md` (verdict must be `PASS`).
- `.specs/_stack.json`
- `.specs/_onboarding.md`
- `.claude/templates/design.template.md`, `.claude/templates/tasks.template.md`, `.claude/templates/adr.template.md`
- Epic mode: `.claude/templates/epic-design.template.md`, `.claude/templates/epic-roadmap.template.md`
- All design/architecture skills above.

## Writes
- `.specs/<feature-id>/03-epic-design.md` (Epic mode)
- `.specs/<feature-id>/03a-epic-roadmap.md` (Epic mode)
- `.specs/<feature-id>/03-design.md`
- `.specs/<feature-id>/04-tasks.md`
- `.specs/<feature-id>/.tdd-state.json` (initial: no `active_task`, every task `phase: "pending"`)
- `.specs/<feature-id>/adr/ADR-NNN-*.md` for any architecturally significant decision.

## Process
1. Refuse if `02-spec-review.md` is missing or its verdict is not `PASS`.
2. Refuse if `01-spec.md` still has any `Q-NNN`.
3. Detect planning mode. Use Epic mode if `--epic` is present or the feature spans multiple vertical slices / shared cross-cutting decisions.
4. In Epic mode, write `03-epic-design.md` and `03a-epic-roadmap.md` first. Refuse to continue if either Epic artifact has unresolved `Q-NNN`.
5. Produce `03-design.md`: module map (`src/<Module>/`) and slice layout (`Features/<Slice>/`), public surface (the module's `Contracts` types — cross-module access goes nowhere else), Minimal API endpoint sketch with `TypedResults` shapes (or the full OpenAPI delta per `openapi-contract-first`), EF Core model and entity configurations, the EF Core migration plan (`dotnet ef migrations add`, reviewed as an explicit pipeline step — never applied at startup), the `ProblemDetails` error model, observability (`ILogger<T>` LoggerMessage events, `ActivitySource` spans, `Meter` counters, health checks), security touch points (authn/authz policy, CORS, rate limit, secrets), and the ArchUnitNET rule additions the slice needs.
6. For each architecturally-significant choice, write an ADR. Every overridden default and every deferred layer gets one.
7. Decompose into tasks `T-001`, `T-002`, ... per `dotnet-task-decomposition`. Each task must list:
   - `id`, `title`, `acs_covered: [AC-NNN, ...]`, `files_in_scope: [paths]`, `depends_on`, `estimated_phases: [red, green, refactor, simplify]`.
   - Tasks that touch `src/<Module>/**` MUST list at least one file under `tests/<Module>.Tests/**` or `tests/<Module>.IntegrationTests/**` in `files_in_scope`.
   - Per-task gates: `unit` always; `it` when the task needs a container or the full host; `coverage` on every task that adds production lines.
8. Validate AC coverage: every AC from `01-spec.md` must appear in at least one task. If not, FAIL the plan and surface the gap.
9. Initialize `.tdd-state.json` — `tasks` is a map keyed by task id, with a top-level `active_task`. An array shape is rejected by the hooks.
   ```json
   { "active_task": null, "tasks": { "T-001": { "phase": "pending", "files_in_scope": [...], "acs_covered": [...] } } }
   ```

## Refuse if
- Spec review verdict is not `PASS`.
- Epic mode is active and either Epic artifact is missing.
- Any AC has no covering task.
- Any task touches `src/<Module>/**` without a corresponding `tests/<Module>.Tests/**` or `tests/<Module>.IntegrationTests/**` file in scope.
- A task's `files_in_scope` would be so broad that `enforce-files-in-scope.sh` stops being a meaningful boundary (e.g. a whole module directory).
- A design decision was made without an ADR where a default is overridden.

## Done when
- `03-design.md`, `04-tasks.md`, any ADRs, and `.tdd-state.json` are written.
- `.tdd-state.json` uses the keyed-map shape with `active_task: null`.
- The user is pointed to `/net-build T-001`.
