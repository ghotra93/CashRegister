# Spec Review: 2026-09-21-cash-register-change-calculation

> Owner: `dotnet-spec-author` (review hat) · Phase 2 · Checklist: `.claude/checklists/spec-review.md`

## Inputs

- `01-spec.md` revision: working tree, 2026-09-21, revision 2. It is untracked, so there is no git SHA. F-001 to F-010 from review 1 and the NQ-001 to NQ-005 answers are applied.

## Summary

| Field | Value |
|---|---|
| verdict | **PASS** |
| acs_total | 28 active (AC-001 to AC-029; AC-014 withdrawn and moved to DC-001) |
| acs_failed | 0 |
| open_questions | 0 |
| next_command | `/net-plan` |

## Checklist

### Source & framing
| Item | Result | Rationale |
|---|---|---|
| Source recorded | pass | Ad-hoc README.md at commit 9b945d3 with a snapshot date. |
| Goal is one paragraph and user-visible | pass | Covers upload, change, and download in the UI. |
| Non-goals present and explicit | pass | v2 deferrals listed, each tied to its question. |
| Glossary covers AC terms | pass | Adds No change, rule priority, line error message, and uploaded file entry. |
| Domain entities section present | pass | |
| Entities in business terms | pass | |
| Relationships have cardinality and meaning | pass | The lowest priority value wins; divisor setting and uploaded file entry added. |

### Acceptance criteria
| Item | Result | Rationale |
|---|---|---|
| Stable `AC-NNN` IDs | pass | AC-014 withdrawn with a note; its ID is not reused. The rest are grouped by topic, and IDs are unchanged. |
| EARS-lite shape | pass | Every AC is ubiquitous, event (When), state (While) or unwanted (If … then). |
| Atomic | pass | The five invalid-line cases are split into AC-017 to AC-021. |
| Testable, one `[Trait("AC", …)]` test each | pass | AC-004 and AC-013 are testable with a deterministic random source plus AC-005. AC-027 and AC-028 are testable at the API and UI levels. |
| No implementation choices | pass | ProblemDetails is the mandated error contract. |
| NFRs measurable | pass | NFR-001 is p95 < 500 ms at 1000 lines. The others are binary (present or absent). |
| Failure paths specified as ProblemDetails | pass | AC-015, AC-023, AC-026. Per-line errors go into the output file by design (Q-005). |

### No-invention
| Item | Result | Rationale |
|---|---|---|
| Assumptions only user/source-stated | pass | |
| No silent defaults | pass | Storage, divisor range, list lifetime, module layout and observability are all answered. |
| All Q-NNN resolved | pass | Q-001 to Q-015 and NQ-001 to NQ-005 are resolved; answers are quoted verbatim. |

### Completeness
| Item | Result | Rationale |
|---|---|---|
| Source ACs all reflected | pass | README samples 1 and 2 are exact ACs; sample 3 is covered by AC-004 and AC-005. |
| Out-of-band inputs recorded | pass | |

### Cutover safety
| Item | Result | Rationale |
|---|---|---|
| Feature flag or waiver | n/a | Greenfield. |

## Findings (review 1, all resolved)

- F-001 (blocker) — ACs for resolved answers → added AC-016 to AC-029. ✅
- F-002 (blocker) — AC-015 vs per-line errors → AC-015 now covers only whole-request failures. ✅
- F-003 (major) — AC-004 hard-coded 3 → uses the active divisor. ✅
- F-004 (major) — AC-003 wording → rewritten. ✅
- F-005 (major) — AC-013 and AC-014 form → AC-013 is now state-driven; AC-014 moved to DC-001. ✅
- F-006 (major) — NFR section → added NFR-001 to NFR-005. ✅
- F-007 (major) — persistence non-goal conflict → reworded to in-memory v1. ✅
- F-008 (minor) — relationships → updated. ✅
- F-009 (minor) — v2 deferrals → listed under Non-Goals. ✅
- F-010 (nit) — dates and placeholder → fixed. The user sign-off box is reset for re-confirmation. ✅

## Notes for design (non-blocking)

- A divisor of 1 is valid (NQ-002), which makes every transaction random. The UI may want to warn about this.
- Per-line error message wording is not specified. The design should define it and test it through AC-017 to AC-020.
- The divisor and file list are in memory (NFR-005), so they assume a single API instance.

## New Questions Raised

- (none)

## Verdict

- [x] Approved — proceed to `/net-plan`
- [ ] Changes requested

Reviewer: dotnet-spec-author (review hat); user confirmation of the revised ACs is pending
Date: 2026-09-21
