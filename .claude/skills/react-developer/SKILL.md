---
name: react-developer
description: React 19 + Next.js (App Router) + TypeScript idioms and defaults — Server vs Client Components, data fetching and caching, Server Actions and forms, routing, client state, styling, accessibility, performance, security, and testing (Vitest, React Testing Library, MSW, Playwright). Use when writing, planning, testing, or reviewing any React / Next.js code.
when_to_use:
  - Phase 3 (Plan) — deciding route map, server/client boundaries, and data-fetch strategy in `03-design.md`.
  - Phase 4 (Build) — writing a page, layout, component, hook, Server Action, or test.
  - Phase 6/7 (Validate, Review) — checking gates and reviewing a React diff.
  - Any question about React, Next.js, JSX/TSX, hooks, or App Router behavior.
authoritative_references:
  - https://react.dev/reference/react
  - https://nextjs.org/docs/app
  - https://testing-library.com/docs/react-testing-library/intro
  - https://tanstack.com/query/latest/docs/framework/react/overview
---

# React / Next.js Developer Guidelines

1. **Detect before advising.** Read `package.json` for the React and Next.js versions and the package manager (lockfile), `next.config.*`, and `tsconfig.json`. App Router guidance here assumes Next.js 15+ and React 19; if the project uses the Pages Router (`pages/`), follow its existing patterns and say so.
2. **Follow the workspace.** Match existing folder structure, styling approach, state library and test setup. Never introduce a second library for a job one already does.
3. **Verify after generating.** Run `tsc --noEmit`, the linter and `next build`; fix errors before declaring done.
4. **No new npm dependencies** without explicit user confirmation.

## Defaults (industry standard for the App Router)

| Concern | Default | Reach for instead when |
|---|---|---|
| Component kind | Server Component | state, effects, event handlers, browser APIs → `'use client'` leaf |
| Reading data | `async` Server Component + `fetch` / data-access function | data must refresh on the client (polling, infinite lists) → TanStack Query |
| Writing data | Server Action + `useActionState` | a non-React client calls it → Route Handler |
| Validation | Zod schema, enforced on the server | — (client validation is UX only) |
| URL/filter state | `searchParams` | — |
| Client UI state | `useState` / `useReducer`, context for small shared state | large cross-cutting client state → Zustand (only if already used or approved) |
| Styling | Whatever the project uses; CSS Modules or Tailwind for new apps | — |
| Unit/component tests | Vitest + React Testing Library + `user-event` + MSW | — |
| E2E | Playwright against `next build && next start` | — |

## Components

- **Server vs Client Components**: boundaries, serializable props, composition patterns, `server-only`. Read [server-and-client-components.md](references/server-and-client-components.md)
- **Component design and hooks**: props typing, composition, hook rules, effects and when not to use them, keys, refs. Read [components-and-hooks.md](references/components-and-hooks.md)

## Data

- **Fetching and caching**: server reads, caching and revalidation, parallel vs sequential fetching, streaming with Suspense, TanStack Query for client-side server state. Read [data-fetching.md](references/data-fetching.md)
- **Mutations and forms**: Server Actions, `useActionState`, `useFormStatus`, `useOptimistic`, schema validation, revalidation after writes. Read [server-actions-and-forms.md](references/server-actions-and-forms.md)

## Routing

- **App Router**: file conventions (`page`, `layout`, `loading`, `error`, `not-found`), dynamic segments, route groups, navigation, Route Handlers, middleware. Read [routing.md](references/routing.md)

## Quality

- **Accessibility**: semantics, labels, keyboard, focus, live regions, testing with accessible queries. Read [accessibility.md](references/accessibility.md)
- **Performance**: bundle size, client boundaries, images and fonts, waterfalls, memoization policy. Read [performance.md](references/performance.md)
- **Security**: env vars, secret handling, action authorization, XSS, redirects, headers. Read [security.md](references/security.md)
- **Styling**: CSS Modules, Tailwind, global styles, theming. Read [styling.md](references/styling.md)

## Testing

- **Testing**: scope selection, Vitest + React Testing Library setup, MSW, testing Server Actions and Server Components, Playwright, AC tagging. Read [testing.md](references/testing.md)

## Tooling

- **Tooling and gates**: package manager detection, scripts, ESLint, TypeScript strictness, the validation gate commands. Read [tooling.md](references/tooling.md)

## Hard rules (shared with the React agents)

- Server Components by default; every `'use client'` has a reason and sits as low in the tree as possible.
- No `useEffect` to fetch data that a Server Component could read.
- Every Server Action validates its input on the server and re-checks authorization.
- Only `NEXT_PUBLIC_*` environment variables in client code; secret-touching modules import `server-only`.
- TypeScript `strict`, no `any`, no `@ts-ignore`.
- Extract any literal used 2+ times in a file to a `const`.
