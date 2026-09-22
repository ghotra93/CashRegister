# 3. Verification: how the AI-generated pieces were confirmed to work

## Method

1. **Tests written before code.** Every task ran red → green → refactor → simplify. A failing test was run and its failure recorded (in `.specs/…/.tdd-state.json` and `05-implementation-log.md`) before any production code was written. Repository hooks blocked production edits without a recorded failure, and blocked edits outside the task's declared files.
2. **Independent gates** after every task, and again at validation: format check, Release build with warnings as errors, unit tests, integration tests, and coverage.
3. **One harness command** for the final verdict (`./.github/scripts/harness-dotnet.sh`): format, compile, unit, integration, coverage (floor 90%), new-code coverage (95%), API contract diff, NuGet/npm vulnerability scan, and web lint/typecheck/tests/build. Latest run: **PASS** at `44f0b86` ([validation report](../../.specs/2026-09-21-cash-register-change-calculation/07-validation-report.md)).
4. **Live checks against the running app**, beyond the automated tests:
   - README sample through `dotnet run` and `curl`;
   - the same through `docker compose` (nginx → API): health, upload, download, divisor change, oversize upload, deep-link fallback, non-root container user;
   - an oversize-upload probe during code review.
5. **Code review** of the whole feature against a rubric, which produced 10 findings; the major one was fixed (T-018).

> **[Author: add your own checks.]** For example: whether you clicked through the UI in a browser, read the code, or ran the tests yourself. The transcript shows the AI's checks; only you can state yours.

## Results (validation revision 2, commit `44f0b86`)

| Check | Result |
|---|---|
| Unit tests (xUnit v3) | 159 / 159 |
| Integration tests (full in-process host) | 30 / 30 |
| Web tests (Vitest + React Testing Library + MSW) | 30 / 30 |
| .NET unit coverage | 100% line, 100% branch |
| Web coverage | 100% line, 90.38% branch |
| New-code coverage | 100% (349/349 lines across the feature) |
| Performance (NFR-001) | a 1000-line file is well under the 500 ms p95 budget (55 uploads ≈ 0.7 s in total) |
| Architecture rules (ArchUnitNET) | 8 rules pass, each "must not" rule with a positive control |
| Vulnerable dependencies | 0 (NuGet and npm) |
| Traceability | every one of the 28 acceptance criteria has at least one tagged test |

## Edge cases tested

**README samples (exact):** `2.12,3.00` → `3 quarters,1 dime,3 pennies`; `1.97,2.00` → `3 pennies`; `3.33,5.00` → random, and the coins always total 1.67.

**Change calculation**
- The fewest-coins result adds up exactly for **every amount from 0 to 1000¢**.
- Random change adds up exactly across **1000 seeds** × amounts 0–500¢; different seeds give different coins; the same seed repeats; coins are listed largest first with no zero counts.
- Divisor rule: 333 and 300 match with divisor 3, and 212 and 197 don't. With divisor 5, 500 matches and 333 doesn't.
- Several matching rules: the lowest priority wins, and a tie goes to the rule registered first.
- A rule pointing at an unregistered change method, or a missing minimal method, fails at startup.
- Exact payment → `No change`.

**Input parsing (per-line errors)**
- Malformed: `abc`, a single value, three values, `;` as separator, empty fields, `$1.00`, `1.`, `.50`, `1,000.00` (thousands separators), two decimal points, letters after the point, a number too big to store.
- Negative amounts on either side; paid < owed; more than 2 decimal places.
- Check order: negative is reported before too-many-decimals.
- Whitespace trimmed; whole dollars (`5,6`) accepted; zero (`0,0`) accepted.
- The decimal separator comes from the currency: a test currency using `'` parses `1'50`, and USD rejects it.

**File processing**
- Blank and whitespace-only lines skipped (including `\r\n` endings).
- Output keeps the input order.
- An invalid middle line gets its error, and the lines after it are still processed.
- Exactly 1000 lines are accepted; 1001 are rejected.
- An empty file gives no output.
- Output is joined with `\n`, with no trailing newline.

**HTTP API**
- No file → 400 `Invalid file`; > 1000 lines → 400 `Too many lines`; unknown file id → 404 `File not found`.
- Divisor 0, -2 or missing → 400 `Invalid divisor`, with the old value kept.
- Divisor `"abc"`, `2.5` or text that isn't JSON → 400 (this caught a real 500 bug, D-4).
- Wrong content type → 415; unhandled exception → 500 with no internal details leaked.
- File names: paths (`..\..\secret\evil.txt`, `../etc/passwd`) stripped, empty names fall back to `upload.txt`, names capped at 100 characters, and the download named `<name>-change.txt`.
- CORS: the UI's development origin is allowed and other origins are refused; Production allows none by default. OpenAPI is served in Development only.
- A divisor change applies to the **next** file. Proved deterministically: after setting 5, `3.33,5.00` must give the exact fewest-coins result.

**UI**
- Upload button disabled until a file is chosen; success summary; the server's error message shown (e.g. `Too many lines`); a network failure shows a generic message.
- A new upload appears in the list without a page reload; the list is newest first, with a download link per file; empty and error states.
- A slow earlier list load cannot overwrite a newer one.
- Divisor: loads the current value, saves, shows the server's rejection message, warns when it's 1, and ignores a load that finishes after the panel is removed.

## Known gaps (recorded, not hidden)

- **Mutation testing** is not set up, so the gate is skipped.
- **Uploads over 1 MB** return a generic "Bad Request" (review F-002, confirmed live, not yet fixed).
- **Web branch coverage** is 90.38%; the uncovered branches are React teardown timing guards.
- **The contract check** compares endpoints (path + method), not request and response shapes.
- **The UI was not driven in a real browser** by the AI; its checks were component tests plus live API calls. **[Author: state whether you did.]**
