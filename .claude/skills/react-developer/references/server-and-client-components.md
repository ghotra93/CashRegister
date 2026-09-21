# Server and Client Components

In the App Router every component is a **Server Component** unless its module (or an ancestor module it is imported into) starts with `'use client'`.

## Choosing

| Needs | Kind |
|---|---|
| Read data, access the database or secrets, keep large dependencies off the client | Server |
| `useState`, `useReducer`, `useEffect`, context providers | Client |
| Event handlers (`onClick`, `onChange`) | Client |
| Browser APIs (`window`, `localStorage`, `IntersectionObserver`) | Client |

## Rules

- **Push the boundary down.** Make the interactive leaf a Client Component, not the page. A page that needs one button should stay a Server Component that renders `<LikeButton />`.
- **`'use client'` marks an entry point, not a single component.** Everything a client module imports becomes client code. Keep client modules small and free of server imports.
- **Props crossing the boundary must be serializable**: plain objects, arrays, strings, numbers, booleans, `Date`, `Map`/`Set`, Promises, and Server Actions. Not class instances, not arbitrary functions.
- **Pass Server Components as `children`** to a Client Component to keep them on the server:

```tsx
// app/dashboard/page.tsx (Server)
export default async function Page() {
  const stats = await getStats();
  return (
    <CollapsiblePanel>          {/* 'use client' */}
      <StatsTable stats={stats} /> {/* stays a Server Component */}
    </CollapsiblePanel>
  );
}
```

- **Guard server code** with the `server-only` package so an accidental client import fails the build:

```ts
// lib/data/orders.ts
import 'server-only';
export async function getOrders() { /* database or secret-bearing fetch */ }
```

- **Context providers are client components.** Wrap them in a small `providers.tsx` with `'use client'` and render it from the root layout around `children`.
- **Third-party components** that use hooks but lack `'use client'` must be re-exported from a client module.

## Review flags

- `'use client'` on a page or layout without a stated reason.
- A client module importing from `lib/data`, `db`, or anything that reads non-`NEXT_PUBLIC_` env vars.
- Non-serializable props (class instances, functions other than Server Actions) passed from server to client.
