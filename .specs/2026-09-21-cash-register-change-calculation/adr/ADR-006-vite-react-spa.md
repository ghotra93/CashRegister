# ADR-006: Vite React SPA (not Next.js)

- **Status:** accepted
- **Date:** 2026-09-21
- **Deciders:** user, `dotnet-architect`

## Context and problem statement

The user wants React + TypeScript. The toolkit's `react-new-app` default is Next.js (App Router). The UI is three widgets calling a .NET API, and needs no SEO or server rendering.

## Considered options

1. Next.js App Router.
2. Vite + React 19 + TypeScript SPA.

## Decision outcome

Chosen option: **Option 2**. This overrides the toolkit default. The reasons: there is no server-side need, the .NET API is already the backend, and a Next.js server would be a second runtime to deploy.

- Tests use Vitest, React Testing Library and MSW. Lint uses ESLint, and type checking uses `tsc --noEmit` in strict mode.
- In development, Vite proxies `/api` to the API. For production, the built `dist/` can be served by any static host or by the API itself (v2 decision).

## Links

- Source: user request "i want .net core 10 API + react + Typescript system"
