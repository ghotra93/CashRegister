# Data fetching and caching

## Server reads (default)

Read data in `async` Server Components, through small `server-only` data-access functions so the query lives in one place and is testable on its own.

```tsx
// lib/data/files.ts
import 'server-only';

const API_BASE = process.env.API_BASE_URL;

export async function getFileStatus(fileId: string): Promise<FileStatus | null> {
  const response = await fetch(`${API_BASE}/api/v1/files/${fileId}`, { cache: 'no-store' });
  if (response.status === 404) return null;
  if (!response.ok) throw new Error(`File status request failed: ${response.status}`);
  return response.json() as Promise<FileStatus>;
}

// app/files/[fileId]/page.tsx
export default async function FilePage({ params }: { params: Promise<{ fileId: string }> }) {
  const { fileId } = await params;
  const file = await getFileStatus(fileId);
  if (!file) notFound();
  return <FileStatusView file={file} />;
}
```

## Caching — decide explicitly for every read

Caching defaults have changed between Next.js versions, so never rely on the default; state the choice.

| Data | Option |
|---|---|
| Per-request / user-specific | `fetch(url, { cache: 'no-store' })` |
| Changes on a known schedule | `fetch(url, { next: { revalidate: 60 } })` |
| Changes when *we* write it | `fetch(url, { next: { tags: ['files'] } })` + `revalidateTag('files')` in the Server Action |
| Non-`fetch` source (ORM, SDK) | wrap with the project's cache helper (`unstable_cache` or `'use cache'` where enabled) or mark the route dynamic |

Record the caching policy for the feature in an ADR.

## Avoid waterfalls

Start independent requests together:

```tsx
const [user, orders] = await Promise.all([getUser(id), getOrders(id)]);
```

Pass a Promise down and `use()` it in a child, or wrap slow sections in `<Suspense>` so the rest of the page streams first. Every route that fetches should have a `loading.tsx` or an explicit Suspense boundary, plus an `error.tsx`.

## Client-side server state — TanStack Query

Use TanStack Query (not `useEffect` + `fetch`) when data must be fetched or refreshed **in the browser**: polling, infinite scroll, refetch on focus, optimistic lists.

```tsx
'use client';
const FILE_STATUS_KEY = 'file-status';

export function useFileStatus(fileId: string) {
  return useQuery({
    queryKey: [FILE_STATUS_KEY, fileId],
    queryFn: () => fetchFileStatus(fileId),
    refetchInterval: 5_000,
  });
}
```

- Consumers and tests target `data`, `isPending`, `isError`, `error`.
- Seed from the server with `initialData` or hydrate with `HydrationBoundary` to avoid a loading flash.
- One `QueryClient` per request on the server and one per browser session, created in a client `providers.tsx`.

## Typing API responses

Type responses at the boundary. For external or untrusted APIs, parse with a Zod schema rather than casting, so a contract change fails loudly at the edge instead of deep in the UI.
