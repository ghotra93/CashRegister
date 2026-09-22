# Ship Plan: 2026-09-21-cash-register-change-calculation

> Owner: `dotnet-code-reviewer` · Phase 8 · Skills: `shipping-and-launch`, `dotnet-security-baseline`, `dotnet-observability-ops`
>
> Pre-deploy hygiene. The agent never deploys; it produces this plan and prints the deploy command for the user.

## Inputs

- Spec: `01-spec.md` (28 active ACs; 0 open questions)
- Design: `03-design.md`, ADR-001 to ADR-009 (ADR-008 amended 2026-09-22)
- Tasks: `04-tasks.md` (T-001 to T-018 done; X-001 registers the files produced outside tasks)
- Validation: `07-validation-report.md` revision 2, **PASS** at `44f0b86`
- Code review: `08-code-review.md`, **Approve** (F-001 resolved by T-018)
- Diff: `origin/main...HEAD` (`4423547...18a1d62`, 11 commits: T-013 to T-018, test phase, harness, Docker, docs). The whole feature was reviewed against `9b945d3`.
- Human inputs (2026-09-22): telemetry rule waived (OTel in v2); deploy to **one container host**; owner and watcher **Harpreet Ghotra**; **no alerts** (demo, no on-call).

## 1. Pre-ship gates

| Gate | Source | Result | Notes |
|---|---|---|---|
| Validation | `07-validation-report.md` | **PASS** | rev 2 at `44f0b86`: 159 unit, 30 integration, coverage 100% / 100%, new code 100% (27/27) |
| Code review | `08-code-review.md` | **Approve** | F-001 major resolved (T-018); 6 minor and 3 nit findings open as recommendations |
| Open questions | spec + design | **0** | — |
| Baseline regression | `.specs/_baseline.json` | **none** | file absent (greenfield); validation finding F-001 |
| Diff scope | `files_in_scope` + `04-tasks.md` X-001 | **in scope** | 0 out-of-scope files after registering X-001 (18 phase-produced files) |
| Migrations | `src/**/Migrations/**` | **n/a** | no database, no EF Core (ADR-002) |
| Contract | `artifacts/openapi/openapi.json` | **PASS** | 0 breaking; 5 operations added (first publication) |
| Instrumentation | diff | **PASS (waived)** | no `ActivitySource`/`Meter`; waived by ADR-008 amendment (user decision 2026-09-22) |
| Security | harness `security` gate | **PASS** | 0 vulnerable NuGet/npm packages; no secrets |

## 2. Feature-flag posture

- **Flag name:** `none`. Reason: this is the first release of a new, standalone application; there is no existing behaviour to protect, and turning it off means stopping the containers.
- **Default in production:** n/a (no flag)
- **Kill-switch:** `docker compose down` on the host (stops both UI and API)
- **Owner:** Harpreet Ghotra
- **Removal plan:** n/a. Reconsider when v2 changes existing behaviour (auth, persistence, a new currency), which will need a flag with a rollback procedure (spec review checklist, "cutover safety").

## 3. Migration safety

| Script | Class | Rollback procedure |
|---|---|---|
| — | n/a | No database and no migrations in v1 (ADR-002). All state (divisor, uploaded files) is in memory and resets on restart. |

- [x] No previously-released migration script was renamed or edited (none exist).
- [x] No `breaking` scripts.
- [x] No `contract` steps.
- Idempotent script (`dotnet ef migrations script --idempotent`): **n/a**, because there is no `DbContext`.

## 4. Observability sign-off

Signals available in v1 (ADR-008): LoggerMessage events `FileProcessed` (file name, line count, error-line count, elapsed ms), `FileRejected`, `DivisorChanged` (old → new), written as JSON to the console outside Development (`docker compose logs api`), and a `/health` liveness endpoint.

| Surface | Metric | Structured-log key | Alert | Dashboard |
|---|---|---|---|---|
| `POST /api/files` | none (OTel deferred, ADR-008) | `FileProcessed` (`FileName`, `LineCount`, `ErrorLineCount`, `ElapsedMs`); `FileRejected` (`FileName`, `Reason`) | **none.** Reason: demo deployment with no on-call (user decision); detection is manual | none; read `docker compose logs api` |
| `GET /api/files` | none (ADR-008) | ASP.NET Core request logs | **none.** Same reason | none |
| `GET /api/files/{id}/output` | none (ADR-008) | ASP.NET Core request logs | **none.** Same reason | none |
| `GET /api/settings/divisor` | none (ADR-008) | ASP.NET Core request logs | **none.** Same reason | none |
| `PUT /api/settings/divisor` | none (ADR-008) | `DivisorChanged` (`OldDivisor`, `NewDivisor`) | **none.** Same reason | none |
| `GET /health` | none | — | **none.** Same reason. Manual check: `curl http://<host>:8080/health` → `Healthy` | none |

Performance budget for reference (NFR-001): p95 < 500 ms for a 1000-line file, verified in CI by `FileUploadPerformanceTests`; in production it can be read from `FileProcessed.ElapsedMs`.

## 5. Rollback plan

1. **Detection.** No automated alerts (user decision). Detection comes from a user or reviewer report, or the owner's post-deploy check: `/health` returns anything but `Healthy`, uploads fail, or the output is wrong for the README sample.
2. **Stop the bleeding (≤ 5 min).** On the host, check out the previous release and rebuild it: `git checkout <previous-release-tag> && docker compose up -d --build`. Or run `docker compose down` to take the service offline.
3. **State recovery.** Nothing to recover: there is no database, and the in-memory divisor and file list reset on restart (ADR-002). Users re-upload any files processed during the bad window; the divisor goes back to the default 3 and must be set again from the UI if it had been changed.

## 6. Staged rollout

Single-instance only: the in-memory divisor and file store (ADR-002) make multiple instances inconsistent, so percentage canaries don't apply.

| Step | Cohort | Entry criteria | Abort criteria | Observation window | Watcher |
|---|---|---|---|---|---|
| Deploy | the one container host (100%) | all gates above PASS; release tagged | `/health` not `Healthy` within 2 min; smoke test fails | — | Harpreet Ghotra |
| Smoke test | the owner, on the host | containers `Up` | README sample output differs in lines 1–2; the upload doesn't return 201; the divisor can't be changed | 10 min | Harpreet Ghotra |
| Steady state | all users | smoke test clean | any user-reported wrong change or failed upload | ongoing, manual | Harpreet Ghotra |

Smoke test (on the host):

```bash
curl -s http://localhost:8080/health                                   # Healthy
curl -s -F "file=@samples/input.txt" http://localhost:8080/api/files   # 201 + summary (note the id)
curl -s http://localhost:8080/api/files/<id>/output                    # 3 quarters,1 dime,3 pennies / 3 pennies / <random, totals 1.67>
```

## 7. Release notes

### External (user-facing) — draft; please edit before publishing

- Upload a transaction file (one `owed,paid` pair per line) and download the change to hand back for every line, e.g. `3 quarters,1 dime,3 pennies`.
- When the amount owed in cents is divisible by the chosen number (3 by default), the change comes in random coins that still add up exactly; you can change that number on the page.
- Invalid lines get a clear message in the output (e.g. "amount paid is less than amount owed") without stopping the rest of the file.

### Internal (engineering)

- **AC covered:** AC-001 to AC-013 and AC-015 to AC-029 (28 active; AC-014 withdrawn → DC-001).
- **Diff summary:** A .NET 10 Minimal API module (`src/CashRegister`) with Currencies and Change slices: integer-cent money; priority-ordered change rules → strategies (minimal and random); strict line parser with per-line errors; a file processor (1000-line cap); in-memory file store and divisor. A thin host (`src/CashRegister.Api`) composed through `HostSetup` (ProblemDetails everywhere, health, CORS allow-list, OpenAPI in Development). A Vite + React 19 + TypeScript UI for upload, the file list with downloads, and the divisor setting. A quality harness (`.github/scripts/harness-dotnet.sh`), and Docker Compose to run the UI behind nginx next to the API.
- **ADRs:** [ADR-001](adr/ADR-001-single-module-thin-host.md) · [ADR-002](adr/ADR-002-in-memory-state.md) · [ADR-003](adr/ADR-003-rules-and-strategies.md) · [ADR-004](adr/ADR-004-currency-registry-usd-only.md) · [ADR-005](adr/ADR-005-security-posture-v1.md) · [ADR-006](adr/ADR-006-vite-react-spa.md) · [ADR-007](adr/ADR-007-output-format-and-line-errors.md) · [ADR-008](adr/ADR-008-observability-v1.md) (amended) · [ADR-009](adr/ADR-009-build-harness-deviations.md)
- **Migration class:** n/a (no database)
- **Flag name:** `none`
- **Dashboard:** none (OTel deferred; logs through `docker compose logs api`)
- **Known limitations (v1):** no authentication; state lost on restart; single instance only; USD only; English output; uploads over 1 MB get a generic 400 (review F-002).
- **Commits:** `git log 9b945d3..HEAD --oneline` (whole feature) · `git log origin/main..HEAD --oneline` (this push)

## 8. Deploy command (for the user to run)

On the container host, from a checkout of the release:

```bash
git fetch && git checkout <release-tag-or-sha>      # e.g. the merge commit of this branch
docker compose up -d --build                         # builds api + web, starts them
docker compose ps                                    # both services "Up"
curl -s http://localhost:8080/health                 # Healthy, then run the smoke test in section 6
```

Before that, from your workstation, publish the branch and tag the release so rollback has a target:

```bash
git push origin main
git tag -a v1.0.0 -m "Cash register v1" && git push origin v1.0.0
```

No migration script to apply first (no database). The agent does not execute any of this; you run it.

## Sign-off

- [x] All gates PASS (instrumentation waived by ADR-008 amendment).
- [x] Flag posture (none), alert posture (none, demo) and rollout (single host) confirmed by a human on 2026-09-22.
- [x] External release notes edited by a human.
- [x] On-call notified: n/a (no on-call); the owner watches the deploy.

Date: 2026-09-22 · Approved-by: Harpreet Ghotra
