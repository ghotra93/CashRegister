# App Router routing

## File conventions

| File | Purpose |
|---|---|
| `page.tsx` | The route's UI; makes the segment publicly routable |
| `layout.tsx` | Shared UI that persists across child navigations |
| `loading.tsx` | Suspense fallback streamed while the segment loads |
| `error.tsx` | Error boundary for the segment (must be a Client Component) |
| `not-found.tsx` | Rendered by `notFound()` |
| `route.ts` | Route Handler (HTTP methods) — cannot sit beside a `page.tsx` |
| `middleware.ts` (project root) | Runs before matching routes; keep it light |

## Segments

- Dynamic: `app/files/[fileId]/page.tsx`. In Next.js 15+, `params` and `searchParams` are Promises — `await` them.
- Catch-all: `[...slug]`; optional catch-all: `[[...slug]]`.
- Route groups `(marketing)` organise files without affecting the URL.
- Private folders `_components` are never routable — use them for colocated components.

## Navigation

- `<Link href="/files">` for navigation; it prefetches in production.
- `useRouter()` (from `next/navigation`) for programmatic navigation in Client Components; `redirect()` in Server Components and Server Actions.
- `usePathname`, `useSearchParams` read the URL in Client Components; wrap components using `useSearchParams` in `<Suspense>`.

## Metadata

Export `metadata` or `generateMetadata` from `page`/`layout` for titles and descriptions; every page needs a meaningful `<title>`.

## Errors and not-found

- Call `notFound()` for a missing resource — do not render an empty page.
- `error.tsx` receives `error` and `reset`; show a recoverable message and never render `error.message` from the server verbatim in production.
- A root `app/global-error.tsx` covers failures in the root layout.

## Middleware

Use for redirects, rewrites, locale detection and coarse auth gating. It is not a substitute for authorization inside Server Actions and Route Handlers.
