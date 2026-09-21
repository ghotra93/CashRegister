---
description: Run /net-spec-review — see .claude/commands/net-spec-review.md for the authoritative spec.
argument-hint: see .claude/commands/net-spec-review.md
agent: dotnet-spec-author
---
# /net-spec-review

**Phase:** 2 — review
**Owning agent:** `.claude/agents/dotnet-spec-author.md` (review hat)
**Skills used:** `ears-spec-authoring`, `requirements-traceability`

## Purpose
Audit `01-spec.md` against the spec checklist and produce `02-spec-review.md` with a pass/fail verdict and a numbered list of required edits.

## Inputs
- `<feature-id>` (positional). (optional; if omitted, use the most recently modified `.specs/<id>/`.)

## Reads
- `.specs/<feature-id>/01-spec.md`
- `.claude/checklists/spec-review.md`
- `.claude/templates/spec-review.template.md`

## Writes
- `.specs/<feature-id>/02-spec-review.md`

## Process
1. Walk every checklist item; for each, record `pass | fail | n/a` plus a one-line rationale.
2. For each `fail`, write a concrete edit (line + replacement) the spec author must apply.
3. Verify EARS form compliance for every AC.
4. Verify each AC is independently testable (no compound criteria) and could be proved by one test carrying `[Trait("AC", "AC-NNN")]`.
5. Verify the non-functional requirements are numeric, and that every failure path is specified as `ProblemDetails`.
6. Verify `## Open Questions` is empty before declaring overall verdict `PASS`.
7. Emit summary: `verdict`, `acs_total`, `acs_failed`, `open_questions`, `next_command`.

## Refuse if
- `01-spec.md` does not exist.
- Any `Q-NNN` is unresolved — verdict must be `FAIL` with the open question list quoted verbatim (the `block-progress-on-open-questions.sh` hook enforces the same rule downstream).

## Done when
- `02-spec-review.md` exists with a verdict and a numbered edit list.
- If verdict is `PASS`, the user is pointed to `/net-plan`. If `FAIL`, they are pointed back to editing `01-spec.md`.
