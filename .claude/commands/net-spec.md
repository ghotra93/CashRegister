---
description: Run /net-spec — see .claude/commands/net-spec.md for the authoritative spec.
argument-hint: see .claude/commands/net-spec.md
agent: dotnet-spec-author
---
# /net-spec

**Phase:** 1 — specify
**Owning agent:** `.claude/agents/dotnet-spec-author.md`
**Skills used:** `ears-spec-authoring`, `issue-tracker-ingestion`, `requirements-traceability`

## Purpose
Turn raw intent (a sentence, a paragraph, or a ticket URL) into a complete EARS-style specification at `.specs/<feature-id>/01-spec.md` for a .NET 10 modular-monolith codebase.

## Inputs
Either:
- Free text describing the feature, OR
- A ticket reference (`JIRA-123`, GitHub issue URL, Linear ID, Azure work item URL).

Required. If a ticket is supplied, fetch it via the configured MCP server (see `.claude/skills/issue-tracker-ingestion/SKILL.md`) and treat its body as the source.

## Reads
- The supplied text or ticket.
- `.specs/_onboarding.md` (for stack context: solution layout, modules, target framework).
- `.specs/_stack.json` (module list, configured harness layers).
- `.claude/templates/spec.template.md`.
- `.claude/checklists/spec-review.md` (so the spec is born review-ready).

## Writes
- `.specs/<feature-id>/01-spec.md` (feature-id = kebab-case slug derived from the title, prefixed with date `YYYY-MM-DD-`).

## Process
1. Derive `<feature-id>`. Refuse if a folder with that id already exists unless the user passes `--continue`.
2. Extract: business goal, primary actor, in-scope, explicitly-out-of-scope. Name the owning module (`src/<Module>/`) and the intended vertical slice (`Features/<Slice>/`) when the source makes it unambiguous; otherwise raise a `Q-NNN`.
3. Write acceptance criteria as `AC-001`, `AC-002`, ... using strict EARS forms (Ubiquitous, Event-driven, State-driven, Unwanted-behavior, Optional). Every AC must be testable by a single xUnit test tagged `[Trait("AC", "AC-NNN")]`.
4. List non-functional requirements (latency, throughput, security posture, observability signals) with concrete numbers. If unknown, file an `Open Question` instead of guessing.
5. Record the error-shape expectation explicitly: every failure path returns `ProblemDetails`. If the source implies another error shape, raise a `Q-NNN` rather than inventing one.
6. Capture `Open Questions` as `Q-001`, `Q-002`. **Never invent answers.** Stop and surface the questions to the user.
7. Render the file using `.claude/templates/spec.template.md`.

## Refuse if
- The input has fewer than 3 distinct nouns/verbs (likely too vague — ask the user to expand).
- Any acceptance criterion would require unstated assumptions.
- An AC is compound (two behaviors in one criterion) — split it instead.

## Done when
- `01-spec.md` exists with at least one AC and zero invented answers.
- All ambiguities are listed under `## Open Questions` with `Q-NNN` IDs.
- The user has been told the next command is `/net-spec-review` (after they answer any `Q-NNN`).
