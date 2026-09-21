# Tooling and gates

## Package manager

Detect it from the lockfile and use it for every command; never mix managers.

| Lockfile | Manager | Run a binary |
|---|---|---|
| `pnpm-lock.yaml` | pnpm | `pnpm exec` / `pnpm <script>` |
| `yarn.lock` | yarn | `yarn <script>` |
| `package-lock.json` | npm | `npx` / `npm run <script>` |
| `bun.lock` / `bun.lockb` | bun | `bunx` / `bun run <script>` |

## Expected `package.json` scripts

```json
{
  "scripts": {
    "dev": "next dev",
    "build": "next build",
    "start": "next start",
    "lint": "eslint . --max-warnings 0",
    "typecheck": "tsc --noEmit",
    "test": "vitest run",
    "test:coverage": "vitest run --coverage",
    "test:e2e": "playwright test"
  }
}
```

If a script is missing, report it; do not add tooling packages without user approval.

## TypeScript

`tsconfig.json` must have `"strict": true`; also recommended: `"noUncheckedIndexedAccess": true`. No `any`, no `@ts-ignore` (use `@ts-expect-error` with a reason only when unavoidable).

## ESLint

Use `eslint-config-next` (includes `react-hooks` and `jsx-a11y` rules) plus `typescript-eslint`. Treat warnings as errors in CI (`--max-warnings 0`). Never disable a rule inline without a comment explaining why.

## Validation gates

| Gate | Command |
|---|---|
| `lint` | `<pm> run lint` |
| `typecheck` | `<pm> run typecheck` |
| `unit` | `<pm> run test:coverage` |
| `build` | `<pm> run build` |
| `e2e` | `<pm> run test:e2e` (when configured) |
| `deps` | `npm audit --audit-level=high` or the manager's equivalent |

## Forbidden to force green

`--passWithNoTests`, `.only`/`.skip` left in code, lowered coverage thresholds, `eslint-disable` without a reason, `ignoreBuildErrors` / `ignoreDuringBuilds` in `next.config`.
