# Spec: 2026-09-21-cash-register-change-calculation — Cash register change calculation

> Owner: `dotnet-spec-author` · Phase 1 · Template: `.claude/templates/spec.template.md`
>
> **No invention.** If something is not in the source, the conversation, or the codebase, it is logged as a `Q-NNN`.

## Source

- Tracker: ad-hoc
- ID: README.md (repository root)
- URL: `README.md`, commit `9b945d3`
- Snapshot date: 2026-09-21
- Snapshot summary:
  > Creative Cash Draw Solutions wants an app that tells the cashier how much change is owed and which denominations to use. In most cases it returns the minimum amount of physical change, but if the "owed" amount is divisible by 3 the denominations are randomly generated (the math must still be right). The program accepts a flat file in which each line holds the amount owed and the amount paid, separated by a comma (e.g. `2.13,3.00`), with multiple lines expected. It outputs the change per line, formatted like `1 dollar,2 quarters,1 nickel`, with each input line producing a new output line. Sample: `2.12,3.00` → `3 quarters,1 dime,3 pennies`; `1.97,2.00` → `3 pennies`; `3.33,5.00` → `1 dollar,1 quarter,6 nickels,12 pennies` (random). Things to consider: changing the random divisor, adding another special case, and a new client in France.

## Goal

A cashier uploads a flat file of transactions (amount owed, amount paid) through a web UI. The system works out, for each transaction, the change to hand the customer as counts of named denominations, and makes the result available as a downloadable output file from the uploaded file's entry in the UI. Change uses the fewest physical pieces, except when the amount owed in cents is divisible by the special-case divisor (3 by default, changeable in the UI); then the denominations are chosen at random but still total the correct change.

## Acceptance Criteria

### File processing
- AC-001: When the cashier submits a flat file of transaction lines, the system shall produce exactly one output line for each non-blank input line.
- AC-002: The system shall preserve the order of the non-blank input lines in the output lines.
- AC-022: When the input file contains blank lines, the system shall ignore them.
- AC-023: If an uploaded file has more than 1000 non-blank lines, then the system shall reject the file with a ProblemDetails response.

### Change calculation
- AC-003: When no special-case rule matches a transaction, the system shall express the change using the minimum number of physical pieces.
- AC-004: When the amount owed in cents is divisible by the active special-case divisor, the system shall select denominations using the random change behaviour.
- AC-005: The system shall make the denominations on every output line sum exactly to the amount paid minus the amount owed.
- AC-013: While the active special-case divisor is N, the system shall apply random change to transactions whose owed amount in cents is divisible by N.
- AC-024: When more than one special-case rule matches a transaction, the system shall apply the rule with the lowest priority value.
- AC-016: When the amount paid equals the amount owed, the system shall output `No change` on that line.

### Output format
- AC-006: The system shall format each change line as comma-separated `<count> <denomination name>` entries.
- AC-007: The system shall list denominations on an output line from largest value to smallest value.
- AC-008: The system shall omit denominations with a count of zero from an output line.
- AC-009: The system shall use the singular denomination name when the count is 1.
- AC-010: The system shall use the plural denomination name when the count is greater than 1.
- AC-011: When the transaction line `2.12,3.00` is processed, the system shall output `3 quarters,1 dime,3 pennies`.
- AC-012: When the transaction line `1.97,2.00` is processed, the system shall output `3 pennies`.

### Invalid lines
- AC-017: If a transaction line cannot be parsed, then the system shall output an error message on that line.
- AC-018: If a transaction line has a negative amount, then the system shall output an error message on that line.
- AC-019: If a transaction line has an amount paid less than the amount owed, then the system shall output an error message on that line.
- AC-020: If a transaction line has an amount with more decimal places than the currency allows, then the system shall output an error message on that line.
- AC-021: If a transaction line is invalid, then the system shall continue processing the remaining lines.
- AC-029: The system shall parse amounts using the decimal separator defined by the transaction's currency.

### Divisor setting
- AC-025: When a user changes the special-case divisor in the UI, the system shall apply the new divisor to files processed afterwards.
- AC-026: If a user submits a divisor that is not an integer greater than or equal to 1, then the system shall reject it with a ProblemDetails response.

### Uploaded files and download
- AC-027: When a file has been processed, the system shall show it as an entry in the UI's uploaded-files list.
- AC-028: When a user selects download on an uploaded-file entry, the system shall return that file's output as a plain-text file.

### Errors
- AC-015: If a request fails as a whole (invalid file, over the line limit, invalid divisor), then the system shall return a ProblemDetails response.

> AC-014 was withdrawn on 2026-09-21 (review finding F-005). Its intent is kept as DC-001. The ID is not reused.

## Design Constraints

- DC-001: Adding a new special-case rule shall not require modifying existing rules or the calculation flow (open/closed). Verified in design and code review. (Source "Things to consider"; Q-008)
- DC-002: Adding a new currency shall not require modifying existing currency handling (open/closed). (Q-002)
- DC-003: Code is organised as a single `CashRegister` module with `Change` and `Currencies` as slices/folders. (Q-014, NQ-004)

## Non-Functional Requirements

- NFR-001: Processing a 1000-line file completes with p95 latency < 500 ms. (Q-011)
- NFR-002: The system writes a structured log entry for every processed file. (Q-015)
- NFR-003: The system exposes a health endpoint that reports liveness. (Q-015)
- NFR-004: No authentication in v1. (Q-012)
- NFR-005: The divisor setting and the uploaded-files list are held in memory and are lost on restart. (NQ-001, NQ-003)

## Domain Entities and Relationships

### Entities

- **Transaction** — purpose: one sale as recorded on one input line; key business attributes: amount owed, amount paid, currency.
- **Change** — purpose: what the cashier returns for a transaction; key business attributes: total amount, list of change entries.
- **Change entry** — purpose: a quantity of one denomination; key business attributes: denomination, count.
- **Denomination** — purpose: a physical note or coin; key business attributes: value, singular name, plural name.
- **Currency** — purpose: the monetary system of a client; key business attributes: code, decimal separator, allowed decimal places, set of denominations.
- **Change rule** — purpose: a special case that changes how denominations are chosen; key business attributes: priority, matching condition (e.g. owed amount divisible by N), behaviour (e.g. random).
- **Divisor setting** — purpose: the currently active special-case divisor; key business attributes: value (integer ≥ 1).
- **Transaction file** — purpose: the flat file the cashier submits; key business attributes: ordered lines.
- **Uploaded file entry** — purpose: a processed file as listed in the UI; key business attributes: file name, upload time, output file.

### Relationships

- **Transaction file 1..* Transaction** — one non-blank line per transaction, in order.
- **Transaction 0..1 Change** — a valid transaction yields exactly one change; an invalid line yields a line error message instead.
- **Change 0..* Change entry** — zero entries when no change is due, which is output as `No change`.
- **Change entry *..1 Denomination** — each entry counts one denomination.
- **Currency 1..* Denomination** — a currency defines its available denominations.
- **Change rule 0..* Transaction** — a rule may match any number of transactions; when several match, the lowest priority value wins.
- **Divisor setting 1..1 Change rule** — the divisor setting drives the divisible-by rule.
- **Uploaded file entry 1..1 Transaction file** and **Uploaded file entry 1..1 Output file** — each entry keeps its input and its downloadable output.

## Non-Goals

- Persisting sales, cash-drawer balances, the divisor, or uploaded files beyond process memory (v1 is in-memory; persistence for N days is v2 — NQ-001, NQ-003).
- Authentication and user management (v2: login page + JWT — Q-012).
- Tracking the physical stock of coins and notes in the drawer (not in source).
- Currencies other than USD (v2; the design must be open/closed for them — Q-002).
- Output language other than English (v2 — Q-003).
- A file status/summary endpoint with row counts (v2 — Q-006).
- Setting the divisor through app configuration (v2 — Q-009).
- OpenTelemetry metrics (v2 — NQ-005).
- Single-transaction entry in the UI (not requested — Q-010).

## Glossary

- **Amount owed** — the price of the sale, the first value on an input line.
- **Amount paid** — what the customer tendered, the second value on an input line.
- **Change** — amount paid minus amount owed.
- **Denomination** — a physical note or coin (dollar, quarter, dime, nickel, penny).
- **Minimum physical change** — the combination with the fewest pieces.
- **Random change behaviour** — denominations chosen at random whose values total the change, with no requirement to differ from minimum change (Q-007).
- **Special-case divisor** — the integer (≥ 1, default 3) that triggers random change when it divides the owed amount in cents.
- **Rule priority** — an integer attached to each change rule; the lowest value wins when several rules match.
- **No change** — the output text when the amount paid equals the amount owed.
- **Line error message** — the text written to an output line in place of change when that input line is invalid.
- **Uploaded file entry** — a processed file listed in the UI, with a download button for its output.
- **Flat file** — a plain-text file with one transaction per line.

## Assumptions

- The initial currency is US dollars, with the denominations dollar, quarter, dime, nickel and penny (source: sample output).
- The system is a .NET 10 API with a React + TypeScript front end (user, in chat).

## Out-of-Band Inputs

- User (2026-09-21): "i want to .net core 10 API + react + Typescript system".
- User (2026-09-21): asked for the spec to be written from the README before any implementation.
- User (2026-09-21): answered NQ-001 to NQ-005 in `02-spec-review.md`.

## Open Questions

- (none)

## Resolved Questions

All answered by the user on 2026-09-21; answers quoted verbatim.

- Q-001: Does "divisible by 3" apply to the owed amount in cents (3.33 → 333) or to whole dollars?
  -> "It applies to the cents."
- Q-002: Which currencies must be supported now for the "new client in France"? Does the currency apply per file, per client, or per line?
  -> "US dollar needs to be supported. we need to have the solution architected in a way that adding a new currency follows open/close principle."
- Q-003: For EUR, what denomination names and output language (English or French) are expected, and are notes included?
  -> "output text be english for now irrespective of the currencies. geospecific/Currencies specific output language be done in v2."
- Q-004: What should the output line be when paid equals owed (no change)?
  -> "output line should have - `No change`."
- Q-005: How are invalid lines handled (malformed, negative amounts, paid < owed, more than 2 decimals)?
  -> "emit the error message on that output line and continue."
- Q-006: Are blank lines in the input ignored, or echoed as blank output lines?
  -> "ignore for now. In V2 we can think of a status call on the file id that returns the detail of the files that processed i.e - noOfRows,emptyRow,noOfRowError,noOfRowSuccessful."
- Q-007: Must random change ever differ from the minimal result, and are there constraints on it?
  -> "random change should not care about it being minimal result but the random change must be equal to the `Change`"
- Q-008: When several special-case rules match one transaction, which wins?
  -> "have a Integer prirotity that is tied to the rules and Lowest Value wins."
- Q-009: How is the divisor or rule set changed?
  -> "I want the divisor to be able to change from the UI in V1. In V2 we can have app configuration"
- Q-010: How is the result delivered?
  -> "a downloadable output file that could be downloaded through a download button on a single entry of the file uploaded."
- Q-011: What are the input limits and performance targets?
  -> "input file limit be 1000 lines, p95<500 ms"
- Q-012: Is authentication required for the API or UI?
  -> "None required in v1. In v2 we could have login page setup for the and jwt based authentication of the user with api."
- Q-013: What input number format is accepted?
  -> "have separator tied with currency. Only supported currency for v1 is us dollar."
- Q-014: Which module and slice own this feature?
  -> See NQ-004.
- Q-015: What observability is required?
  -> See NQ-005: "structured logs + a health check"; OpenTelemetry deferred to v2.
- NQ-001: Where does a UI-changed divisor persist, and what is its scope?
  -> "In memory is fine for v1.for V2 we can persist in db."
- NQ-002: Which divisor values are valid?
  -> "any integer ≥ 1"
- NQ-003: How long is the uploaded-files list kept?
  -> "in memory for v1. we can do persisted for N number of days in V2."
- NQ-004: Single module or separate modules?
  -> "module with `Change` and `Currencies` as slices/folders."
- NQ-005: OpenTelemetry metrics in v1?
  -> "OpenTelementy in v2"

## Sign-off

- [x] All AC are atomic and testable.
- [x] All `Q-NNN` are resolved or explicitly deferred-with-rationale.
- [x] Source recorded.
- [ ] Reviewed by user on <YYYY-MM-DD> (pending re-confirmation of the revised ACs).
