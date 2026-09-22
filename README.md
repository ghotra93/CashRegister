# Cash Register

## The Problem
Creative Cash Draw Solutions is a client who wants to provide something different for the cashiers who use their system. The function of the application is to tell the cashier how much change is owed, and what denominations should be used. In most cases the app should return the minimum amount of physical change, but the client would like to add a twist. If the "owed" amount is divisible by 3, the app should randomly generate the change denominations (but the math still needs to be right :))

Please write a program which accomplishes the clients goals. The program should:

1. Accept a flat file as input
	1. Each line will contain the amount owed and the amount paid separated by a comma (for example: 2.13,3.00)
	2. Expect that there will be multiple lines
2. Output the change the cashier should return to the customer
	1. The return string should look like: 1 dollar,2 quarters,1 nickel, etc ...
	2. Each new line in the input file should be a new line in the output file

## Sample Input
2.12,3.00

1.97,2.00

3.33,5.00

## Sample Output
3 quarters,1 dime,3 pennies

3 pennies

1 dollar,1 quarter,6 nickels,12 pennies

*Remember the last one is random

## The Fine Print
Please use whatever technology and techniques you feel are applicable to solve the problem. We suggest that you approach this exercise as if this code was part of a larger system. The end result should be representative of your abilities and style.

Please fork this repository. When you have completed your solution, please issue a pull request to notify us that you are ready.

Have fun.

## Things To Consider
Here are a couple of thoughts about the domain that could influence your response:

* What might happen if the client needs to change the random divisor?
* What might happen if the client needs to add another special case (like the random twist)?
* What might happen if sales closes a new client in France?

---

# Solution

A .NET 10 Minimal API with a React + TypeScript front end. The cashier uploads a transaction file, sees it in a list, and downloads the change for every line. The special-case divisor can be changed from the UI.

The requirements, design, decisions and build log live in [`.specs/2026-09-21-cash-register-change-calculation/`](.specs/2026-09-21-cash-register-change-calculation/). Start with `01-spec.md` and `03-design.md`.

## Running it

### With Docker (both projects together)

```bash
docker compose up --build
```

- UI: http://localhost:8080. nginx serves the React app and forwards `/api` and `/health` to the API, so the browser uses a single origin and no CORS setup is needed.
- API: http://localhost:5080 (direct access, e.g. for `curl`). It runs in Production mode as a non-root user, so the OpenAPI document is not exposed.
- Stop with `docker compose down`. Uploaded files and the divisor live in memory and reset when the API container restarts (ADR-002).

### Locally

Prerequisites: .NET SDK 10.0.1xx or later, and Node 24.

```bash
# API on http://localhost:5080
dotnet run --project src/CashRegister.Api

# In a second terminal: UI on http://localhost:5173 (proxies /api to the API)
cd web
npm install
npm run dev
```

Open http://localhost:5173 and upload [`samples/input.txt`](samples/input.txt), the README sample. The downloaded output looks like:

```
3 quarters,1 dime,3 pennies
3 pennies
<random denominations totalling 1.67>
```

The API can also be called directly:

```bash
curl -F "file=@samples/input.txt" http://localhost:5080/api/files         # 201 + file summary
curl http://localhost:5080/api/files/<id>/output                            # the change, one line per input line
curl -X PUT -H "Content-Type: application/json" -d '{"divisor":5}' http://localhost:5080/api/settings/divisor
```

## Tests

```bash
dotnet test --solution CashRegister.slnx                                # unit + integration (incl. p95 < 500 ms for 1000 lines)
dotnet test --solution CashRegister.slnx --filter-trait "Category=Integration"   # integration only
npm --prefix web test                                                   # UI and API client (Vitest + MSW)
npm --prefix web run lint && npm --prefix web run typecheck
```

Every test that proves an acceptance criterion is tagged with its ID: `[Trait("AC", "AC-011")]` in C#, and `[AC-027] …` in the name of a TypeScript test.

## Behaviour

| Input line | Output line |
|---|---|
| `2.12,3.00` | `3 quarters,1 dime,3 pennies` (fewest pieces) |
| `3.33,5.00` | random denominations that still total 1.67, because 333 cents is divisible by 3 |
| `2.00,2.00` | `No change` |
| `abc` | `Error: invalid line, expected '<owed>,<paid>'` |
| `-1.00,2.00` | `Error: amounts must not be negative` |
| `3.00,2.00` | `Error: amount paid is less than amount owed` |
| `1.001,2.00` | `Error: amounts must have at most 2 decimal places` |

- Blank lines are ignored, and every other line produces exactly one output line, in order.
- An invalid line gets an error message and processing continues.
- A file with more than 1000 non-blank lines is rejected with a `400` ProblemDetails.
- All money is handled as whole cents (`long`), never floating point.
- v1 keeps the divisor and uploaded files in memory (lost on restart) and has no authentication; both are planned for v2.

## Code layout

```
src/CashRegister/                  the module: all business logic and its endpoints
  Features/Currencies/             Currency, Denomination, UsdCurrency, CurrencyRegistry
  Features/Change/                 Transaction, strategies, rules, parser, formatter, file processor
    Files/  Settings/              the /api/files and /api/settings/divisor endpoints
src/CashRegister.Api/              thin host: ProblemDetails, health, CORS, OpenAPI
tests/CashRegister.Tests/          unit + ArchUnitNET architecture rules
tests/CashRegister.IntegrationTests/  WebApplicationFactory tests + NFR-001 performance test
web/                               Vite + React 19 + TypeScript UI
```

## Things to consider — answered

**Changing the random divisor.** The divisor is a runtime setting (`IDivisorSettings`), edited from the UI or with `PUT /api/settings/divisor`. The rule reads the current value on every transaction, so a change applies to the next file with no redeploy. Any whole number ≥ 1 is accepted; a divisor of 1 makes every transaction random, and the UI warns about it. Persisting the setting is a v2 item: swap in a database-backed `IDivisorSettings`. See ADR-002.

**Adding another special case.** Special cases are `IChangeRule` implementations: each has a `Priority`, a `Matches(transaction)` check, and the change strategy (`IChangeStrategy`) to use. `ChangeCalculator` applies the matching rule with the **lowest** priority value, and falls back to minimal change. A new twist means writing one sealed rule class (and a new strategy if it needs one) and registering it in `CashRegisterModule`; no existing code changes. An architecture test requires every rule to be sealed and to live in `Features/Change/Rules`. See ADR-003.

**A new client in France.** Currencies are subclasses of `Currency`, each holding its code, decimal separator, decimal places and denominations, and they are looked up through `CurrencyRegistry`. Supporting euros means adding a `EurCurrency` class and one DI registration, then setting `CashRegister:Currency` to `EUR`; an unknown code stops the app at startup. The line parser already takes the decimal separator from the currency. Two points are left for v2 (see ADR-004):
- **Separator clash:** a currency that uses `,` as its decimal separator conflicts with the `,` between the two amounts, so the field separator would also need to become a currency setting.
- **Language and currency choice:** output is English-only in v1, and choosing a currency per file or per client needs its own spec.

## Key decisions

| ADR | Decision |
|---|---|
| 001 | One `CashRegister` module (slices as folders) + a thin API host |
| 002 | In-memory divisor and file store for v1; no database |
| 003 | Priority-ordered rules → strategies; integer cents; how random change is generated |
| 004 | Currency registry; v1 processes files in USD |
| 005 | No auth in v1; CORS limited to the UI origin; 1 MB uploads; sanitised file names |
| 006 | Vite React SPA rather than Next.js |
| 007 | Output format (`\n`-joined, `No change`) and exact error wording |
| 008 | Structured logs + health check in v1; OpenTelemetry in v2 |