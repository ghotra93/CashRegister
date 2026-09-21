# Testing

## Stack

- **Vitest** + `@vitejs/plugin-react` + `jsdom` for unit and component tests.
- **React Testing Library** (`@testing-library/react`, `@testing-library/user-event`, `@testing-library/jest-dom`).
- **MSW** (Mock Service Worker) to mock HTTP at the network boundary, in tests and optionally in development.
- **Playwright** for end-to-end tests against a production build.

## Scope selection

| Behavior | Scope |
|---|---|
| Pure function, schema, formatter | Unit (Vitest) |
| Client Component rendering and interaction | Component (RTL + user-event) |
| Hook | `renderHook` |
| Server Action / data-access function | Unit — call it directly; MSW for its HTTP calls; mock `next/cache` / `next/navigation` |
| Synchronous Server Component | Component — render its JSX |
| **Async** Server Component, routing, middleware, streaming, cookies/headers | E2E (Playwright) |

Vitest cannot render async Server Components; do not force it — cover them with Playwright and unit-test the data functions they call.

## Setup sketch

```ts
// vitest.config.mts
import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import tsconfigPaths from 'vite-tsconfig-paths';

export default defineConfig({
  plugins: [tsconfigPaths(), react()],
  test: {
    environment: 'jsdom',
    setupFiles: ['./vitest.setup.ts'],
    coverage: { provider: 'v8', reporter: ['text', 'json-summary', 'lcov'] },
  },
});
```

```ts
// vitest.setup.ts
import '@testing-library/jest-dom/vitest';
import { afterAll, afterEach, beforeAll } from 'vitest';
import { server } from './test/msw-server';

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());
```

## Writing tests

```tsx
const UPLOAD_BUTTON = { name: 'Upload' };

it('AC-017: shows an error for a non-positive divisor', async () => {
  const user = userEvent.setup();
  render(<UploadForm />);

  await user.type(screen.getByLabelText('Divisor'), '0');
  await user.click(screen.getByRole('button', UPLOAD_BUTTON));

  expect(await screen.findByRole('alert')).toHaveTextContent('positive whole number');
});
```

- Query by role, label, then text; `getByTestId` is the last resort.
- `userEvent.setup()` per test; `await` every interaction.
- Wait with `findBy*` / `waitFor`; never `setTimeout` sleeps.
- `onUnhandledRequest: 'error'` makes an unmocked request fail the test.
- TanStack Query: a fresh `QueryClient` per test with `retry: false`.
- Cover loading, error and empty states for every data view, invalid input for every validation rule, and rollback for every optimistic update.

## AC traceability

Prefix the test name with its AC id: `it('AC-004: renders lines in file order', ...)`. Playwright: add `{ tag: '@AC-004' }` or the same prefix. The validator maps these to `07a-traceability.md`; a UI-facing AC with no tagged test is a gap.

## Playwright

- Run against `next build && next start` (configure `webServer` in `playwright.config.ts`), not the dev server.
- Use role-based locators (`page.getByRole`), web-first assertions (`await expect(locator).toBeVisible()`), and isolated test data per test.
