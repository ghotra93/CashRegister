# Performance

Measure first (`next build` output, Lighthouse, React DevTools Profiler, Web Vitals); fix what the numbers show.

## Biggest wins

1. **Keep code on the server.** Every `'use client'` module and everything it imports ships to the browser. Move boundaries down; keep heavy libraries (markdown, date, charting) in Server Components when they only render output.
2. **Avoid request waterfalls.** Start independent fetches with `Promise.all`; stream slow sections behind `<Suspense>`.
3. **Cache deliberately.** Static or revalidated reads are far cheaper than per-request rendering; see data-fetching.
4. **Images and fonts.** `next/image` with explicit `width`/`height` (or `fill` + `sizes`) prevents layout shift; `priority` only for the LCP image. `next/font` self-hosts fonts with no layout shift.
5. **Split rarely used client code** with `next/dynamic` (modals, editors, charts below the fold).

## Rendering

- The React Compiler, when enabled, memoizes automatically — do not add `useMemo`/`useCallback`/`memo` by reflex.
- Without the compiler, memoize only what the Profiler shows is expensive or what must keep a stable identity for a dependency array.
- Stable `key`s prevent needless remounts; unstable ones (random, index on reorderable data) cause them.
- Virtualize very long lists.

## Review flags

- A page-level `'use client'`.
- `useEffect` fetching data on mount.
- Sequential `await`s of independent requests.
- Raw `<img>` for content images; web fonts loaded via `<link>`.
- Large dependencies imported into client modules.
