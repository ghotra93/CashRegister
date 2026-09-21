---
name: react-new-app
description: Creates a new Next.js (App Router) + React + TypeScript app with create-next-app and wires the toolkit's test and quality tooling (Vitest, React Testing Library, MSW, Playwright, strict TypeScript, ESLint). Use whenever a user wants to create a new React or Next.js application.
when_to_use:
  - The user asks to create, scaffold, or bootstrap a new React or Next.js app.
  - A spec's design needs a new frontend workspace before the first frontend task can run.
authoritative_references:
  - https://nextjs.org/docs/app/api-reference/cli/create-next-app
  - https://nextjs.org/docs/app/guides/testing/vitest
  - https://nextjs.org/docs/app/guides/testing/playwright
  - .claude/skills/react-developer/SKILL.md
---

# React New App (Next.js App Router)

1. **Check prerequisites.** Confirm Node.js LTS is installed (`node --version`) and which package manager the user prefers (default `npm`; use `pnpm`/`yarn`/`bun` only if the user or the repo already uses it).

2. **Choose the name and location.** Suggest a name from the user's description or ask. Confirm the target directory does not already exist.

3. **Create the app** non-interactively:

   ```bash
   npx create-next-app@latest <app-name> \
     --typescript --eslint --app --src-dir --import-alias "@/*" \
     --tailwind --use-npm --yes
   ```

   Adjust from the user's needs: `--no-tailwind` if they want CSS Modules only; `--use-pnpm` / `--use-yarn` / `--use-bun` to match their manager; `--turbopack` if they ask for it. Do not pass `--skip-install`.

4. **Harden TypeScript.** Confirm `"strict": true` in `tsconfig.json` and add `"noUncheckedIndexedAccess": true`.

5. **Add the test and quality tooling** — this adds dev dependencies, so list them and get the user's confirmation first:

   ```bash
   npm install -D vitest @vitejs/plugin-react jsdom vite-tsconfig-paths @vitest/coverage-v8 \
     @testing-library/react @testing-library/dom @testing-library/user-event @testing-library/jest-dom \
     msw @playwright/test
   npx playwright install --with-deps chromium
   ```

   Then create `vitest.config.mts`, `vitest.setup.ts`, `test/msw-server.ts`, and `playwright.config.ts` (with `webServer` running `npm run build && npm run start`) as shown in the react-developer skill's testing reference, and add the scripts from its tooling reference.

6. **Add runtime libraries only when the design needs them**, each with user confirmation: `zod` (Server Action validation — almost always), `@tanstack/react-query` (only if the design has client-side server state), `server-only` (as soon as a module touches secrets or the database).

7. **Verify.** Run `npm run lint`, `npm run typecheck`, `npm run test`, and `npm run build`. Fix any failure before handing over. Add one smoke test (the home page renders its heading) so the unit gate has something to run.

8. **Do not start the dev server** unless the user asks. Suggest `npm run dev` once features exist.

## Conventions for generated code

- App code under `src/app/`; shared components under `src/components/`; server-only data access under `src/lib/data/` (each file starts with `import 'server-only'`); Server Actions in `actions.ts` next to the route that uses them.
- Tests colocated as `*.test.ts(x)`; Playwright specs under `e2e/`.
- Follow `react-developer` for everything after scaffolding.
