# 4. Tools and task mapping: how AI was used across the problem

## Tools

| Tool | Used for |
|---|---|
| **Claude Code** (Anthropic), in the VS Code extension; model **Claude Opus 5** | Everything in this repository: requirements drafting, design, code, tests, harness, Docker, documentation. Two sessions (see [01-transcript.md](01-transcript.md)). |
| A **spec-driven toolkit** in `.claude/` (slash commands, agent definitions, skills, hooks) | It gives the AI a fixed workflow and guard rails: spec → review → plan → TDD build → test → validate → review → ship. The hooks enforce the rules mechanically: no production code without a recorded failing test, no edits outside a task's declared files, no skip flags. |
| Git, .NET 10 SDK, Node 24, Docker | Run by the AI through its shell tool, for builds, tests, live probes and Docker runs. **Commits were made by the developer**: the repo settings deny `git commit` to the AI. |

> **[Author: add any other AI tools you used outside this chat]** (for example ChatGPT, Copilot, or a different model to review), or state "none".

## How the AI was used differently at each stage

| Stage | Command / mode | AI's role | Developer's role | Output |
|---|---|---|---|---|
| Understand the problem | Plan mode | Read the README and proposed a first plan | Approved the plan, then **stopped** the AI's first direct coding attempt (decision log R-1) | Plan, then abandoned |
| Specify | `/net-spec` | Drafted testable acceptance criteria and **15 open questions** instead of guessing | **Answered all 15** in the spec file | `01-spec.md` |
| Review the spec | `/net-spec-review` ×2 | Audited the spec against a checklist, found gaps, raised 5 follow-up questions, rewrote the criteria after the answers | Answered NQ-001 to NQ-005 | `02-spec-review.md` (FAIL, then PASS) |
| Design and plan | `/net-plan` | Wrote the design, **8 ADRs**, and 16 tasks each tied to criteria and test files | Signed off | `03-design.md`, `adr/`, `04-tasks.md` |
| Build (TDD) | `/net-build T-001` … `T-018` | For each task: failing test → minimum code → refactor → simplify → gates; logged every phase, and escalated whenever scope or a decision was needed | Committed each task; answered scope and package questions | `src/`, `tests/`, `web/`, `05-implementation-log.md` |
| Harden tests | `/net-test` | Merged unit and integration coverage, found gaps, added cross-cutting suites, built the traceability matrix | Approved or declined packages (Verify, coverage-v8) | `06-test-plan.md`, `07a-traceability.md` |
| Quality harness | `dotnet-build-harness` skill | Measured the cost of the skill's defaults with a throwaway build, then asked instead of imposing them; wrote the gate scripts | Made 4 decisions (coverage source, analyzers, naming, `--no-build`) | `.github/scripts/`, ADR-009 |
| Validate | `/net-validate` ×3 | Ran every gate fresh, wrote a PASS/FAIL report with the numbers | — | `07-validation-report.md` |
| Review | `/net-review` | Reviewed its own code **adversarially** against a rubric (10 findings) and probed the live API | Chose "Fix it" for the major finding | `08-code-review.md`, then T-018 |
| Ship | `/net-ship` ×2 | Refused while gates failed; wrote the ship plan, release notes and deploy commands, and **never deployed** | Waived tracing/metrics, picked the deploy target, owner and alert posture; edited the release notes | `09-ship-plan.md` |
| Extras (ad hoc) | chat requests | Docker Compose (built and smoke-tested live), README updates, PR description, this documentation | Asked for them | `docker-compose.yml`, `README.md`, `docs/ai-usage/` |

## Task → commit map

| Task | What | Commit |
|---|---|---|
| T-001 | Solution, module, host and test projects | `f22e8fb` |
| T-002 | Currencies: Denomination, Currency, USD, registry | `2d5a310` |
| T-003 | Transaction + fewest-coins strategy | `b120061` |
| T-004 | Random change strategy | `ea400b3` |
| T-005 | Rules, divisor setting, ChangeCalculator | `beede55` |
| T-006 | Output formatter | `f830502` |
| T-007 | Line parser with per-line errors | `c2a9fe0` |
| T-008 | File processor + logging | `51c58d8` |
| T-009 | Module registration + API host | `548d81b` |
| T-010 | Upload / list / download endpoints | `f314b49` |
| T-011 | Divisor endpoints (+ fix for the 500 bug) | `455c692` |
| T-012 | Architecture rules + performance test | `4423547` |
| T-013 | Web scaffold + typed API client | `3809825` |
| T-014 | Divisor settings UI | `45f5909` |
| T-015 | Upload, file list and download UI | `d0b8f39` |
| T-016 | README solution section + sample input | `fcb7a26` |
| `/net-test` | Production-host, OpenAPI and cycle tests; test plan | `1032f4a`, `eb7c92c` |
| Harness | Gate scripts, ADR-009, plan T-017 | `d58d54c` |
| T-017 | Endpoint handler, route-table and host tests | `b135f84` |
| T-018 | `Program.cs` → `HostSetup` (review F-001) + Docker | `44f0b86` |
| Docs | Validation, review, ship plan | `7d3b8bb`, `18a1d62`, `b12fe64` |

## Patterns in how AI was used

- **The AI asked instead of guessing** on anything that was a product, legal or cost decision: 20 spec questions, package approvals, the Verify licence, alert posture, deploy target. The developer's answers are in the specs and the decision log.
- **Guard rails were mechanical, not left to the AI's discipline.** Hooks enforced TDD and file scope. When a hook was itself buggy on Windows, the AI stopped and asked rather than working around it (decision log D-2).
- **The AI verified its own output skeptically.** It used positive-control architecture tests, live probes of the running API, and a rubric review of its own code, and fixed the problems it found (decision log section D).
