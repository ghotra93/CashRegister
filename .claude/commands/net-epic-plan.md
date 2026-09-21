---
description: Run /net-epic-plan — see .claude/commands/net-epic-plan.md for the authoritative spec.
argument-hint: see .claude/commands/net-epic-plan.md
agent: dotnet-architect
---
# /net-epic-plan

**Phase:** 3a — Epic planning
**Owning agent:** `.claude/agents/dotnet-architect.md`
**Skills used:** `epic-slicing-planning`, `dotnet-10-conventions`, `openapi-contract-first`, `dotnet-architecture-rules`, `adr-authoring`

## Purpose
Create high-level Epic planning artifacts before detailed slice-level tasks, so the module boundaries and shared cross-cutting decisions are settled once rather than per slice.

## Inputs
- `<feature-id>`. Required.

## Reads
- `.specs/<feature-id>/01-spec.md`
- `.specs/<feature-id>/02-spec-review.md` (verdict must be `PASS`)
- `.specs/_stack.json`
- `.claude/templates/epic-design.template.md`
- `.claude/templates/epic-roadmap.template.md`

## Writes
- `.specs/<feature-id>/03-epic-design.md`
- `.specs/<feature-id>/03a-epic-roadmap.md`
- `.specs/<feature-id>/adr/ADR-NNN-*.md` (if needed)

## Process
1. Refuse if spec review is not `PASS`.
2. Refuse if `01-spec.md` has unresolved `Q-NNN`.
3. Produce `03-epic-design.md`: module boundaries (`src/<Module>/`) and which types belong in each module's `Contracts` surface, shared architecture decisions (DbContext ownership, authn/authz posture, OpenAPI contract ownership, ArchUnitNET rules the Epic adds), integration points, risks, and ADR links.
4. Produce `03a-epic-roadmap.md`: the vertical slices (`Features/<Slice>/`), dependency order, milestone intent, and rollout strategy (including whether slices ship behind a flag).
5. Raise `Q-NNN` for any unresolved Epic-level decision and halt before detailed task decomposition.

## Refuse if
- `02-spec-review.md` verdict is not `PASS`.
- Any AC in `01-spec.md` is unaccounted for in the Epic design/roadmap.
- Epic-level `Q-NNN` remains unresolved at handoff.
- A slice in the roadmap would need to reach into another module's internals rather than its `Contracts` surface.

## Done when
- `03-epic-design.md` and `03a-epic-roadmap.md` exist and are internally consistent.
- Every AC maps to at least one roadmap slice.
- The next recommended command is `/net-plan`.
