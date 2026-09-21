---
description: Run /net-ship — see .claude/commands/net-ship.md for the authoritative spec.
argument-hint: see .claude/commands/net-ship.md
agent: dotnet-code-reviewer
---
# /net-ship

**Phase:** 8 — ship (post-commit, pre-deploy hygiene)
**Owning agent:** `.claude/agents/dotnet-code-reviewer.md` (reused; no new persona)
**Skills used:** `shipping-and-launch`, `dotnet-security-baseline`, `efcore-10-data-access`, `dotnet-observability-ops`

## Purpose
After `/net-review` approves the diff and the user commits, produce a structured ship plan that verifies pre-deploy gates, captures rollback + observability + flag posture, plans the staged rollout, and drafts release notes. The agent **never deploys** — it prints the deploy command for the user to run.

## Inputs
- `<feature-id>` (optional; defaults to the most recent feature with an `08-code-review.md` verdict of `Approve` or `Approve with waivers`).
- `--base <ref>` (optional; defaults to `origin/main`) for diff and release-note generation.

## Reads
- `.specs/<feature-id>/01-spec.md` — `## Goal`, AC list, any open `Q-NNN`.
- `.specs/<feature-id>/03-design.md` — EF Core migration plan, security posture, any open `Q-NNN`.
- `.specs/<feature-id>/04-tasks.md` — `files_in_scope` for the diff-scope check.
- `.specs/<feature-id>/07-validation-report.md` — verdict must be `PASS`.
- `.specs/<feature-id>/08-code-review.md` — verdict must be `Approve` or `Approve with waivers`.
- `.specs/_baseline.json` — for the regression check.
- `artifacts/harness-summary.json`, `artifacts/openapi/openapi.json` — gate results and the shipped contract.
- `git diff <base>...HEAD` and `git log <base>..HEAD` — for migration classification, new-endpoint detection, release-note drafting.
- `.claude/skills/shipping-and-launch/SKILL.md` (authoritative behavior).
- `.claude/templates/ship-plan.template.md`.

## Writes
- `.specs/<feature-id>/09-ship-plan.md`

## Process
1. **Resolve feature.** If no `<feature-id>`, pick the most recent feature with `08-code-review.md` verdict `Approve*`. Refuse if none.
2. **Verify pre-ship gates** (per `shipping-and-launch`): validation `PASS`, review `Approve*`, zero unresolved `Q-NNN`, no baseline regression, every changed file in some task's `files_in_scope`. Halt at the first FAIL with the specific recovery command.
3. **Classify migrations.** Walk the EF Core migration classes in the diff (`src/<Module>/**/Migrations/**`). Tag each: forward-only safe / expand / contract / breaking. Generate the idempotent script the operator will apply — `dotnet ef migrations script --idempotent` — and attach it to the plan; migrations are never applied at app startup. Halt on `breaking` without an ADR.
4. **Inventory new surface.** New Minimal API endpoints, message handlers, background services, typed `HttpClient`s. For each, verify the diff registers instrumentation — an OpenTelemetry `ActivitySource` span and a `Meter` counter — and that liveness/readiness HealthChecks still cover any new dependency. Halt on missing instrumentation.
5. **Render the ship plan.** Fill `.claude/templates/ship-plan.template.md` — every section non-empty (`n/a` allowed only with a one-line reason).
6. **Surface human-required inputs as `Q-NNN`.** Flag owner, alert thresholds, rollout cohorts. Halt until the user answers; do not invent.
7. **Draft release notes.** Two sections: external (plain English, ≤ 3 bullets, edited by the user before publishing) and internal (diff summary, AC list, ADR links, migration class, flag name, dashboard).
8. **Print deploy command.** Suggest the team's release-pipeline trigger, or the `dotnet publish -c Release` / container image build-and-push line, plus the `dotnet ef migrations script --idempotent` output to apply first. The agent never executes any of it.

## Refuse if
- `07-validation-report.md` verdict is not `PASS`.
- `08-code-review.md` verdict is not `Approve` or `Approve with waivers`.
- Any `Q-NNN` is unresolved in the spec or design.
- A migration in the diff is `breaking` and there is no covering ADR.
- A new endpoint, handler, or background service has no `ActivitySource` span and `Meter` registration in the diff.
- The diff edits a previously-released EF Core migration (a rename or a changed `Up`/`Down` body for a migration already applied in any environment).
- The OpenAPI contract diff reports a breaking change with no covering ADR and no versioning plan.
- The diff is empty (nothing to ship).

## Done when
`09-ship-plan.md` exists with all seven sections filled, every gate in the pre-ship table is `PASS`, migrations are classified with named rollback steps and an idempotent script, every new endpoint has at least one named metric and one alert (or a documented "no alert" reason), and release notes have both external and internal sections. The agent then prints the suggested deploy command for the user to execute manually — it never deploys.
