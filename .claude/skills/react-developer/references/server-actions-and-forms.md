# Server Actions and forms

## Server Actions

A Server Action is an `async` function marked `'use server'`. It runs on the server and can be passed to a form's `action` or called from a Client Component.

Every action must:

1. **Validate input on the server** with a schema — the client can send anything.
2. **Re-check authentication and authorization** — an action is a public POST endpoint.
3. **Return a typed result** for expected failures instead of throwing.
4. **Revalidate** what it changed (`revalidatePath` / `revalidateTag`), or `redirect` on success.

```ts
// app/files/actions.ts
'use server';
import { z } from 'zod';
import { revalidateTag } from 'next/cache';

const DivisorSchema = z.coerce.number().int().positive();

export interface UploadState {
  fileId?: string;
  error?: string;
}

export async function uploadFile(_previous: UploadState, formData: FormData): Promise<UploadState> {
  const divisor = DivisorSchema.safeParse(formData.get('divisor') ?? 3);
  if (!divisor.success) return { error: 'Divisor must be a positive whole number.' };

  const response = await postFile(formData.get('file'), divisor.data);
  if (!response.ok) return { error: await readProblemTitle(response) };

  revalidateTag('files');
  return { fileId: (await response.json()).fileId };
}
```

## Forms

```tsx
'use client';
import { useActionState } from 'react';

const INITIAL_STATE: UploadState = {};

export function UploadForm() {
  const [state, formAction, isPending] = useActionState(uploadFile, INITIAL_STATE);
  return (
    <form action={formAction}>
      <label htmlFor="file">Transactions file</label>
      <input id="file" name="file" type="file" required />
      <button type="submit" disabled={isPending}>{isPending ? 'Uploading…' : 'Upload'}</button>
      {state.error && <p role="alert">{state.error}</p>}
    </form>
  );
}
```

- `useFormStatus` gives a nested submit button access to the pending state.
- `useOptimistic` shows the expected result immediately; the test plan must cover the rollback when the action fails.
- Forms built on `action` work before JavaScript loads (progressive enhancement); do not break that with `preventDefault` unless required.
- Client-side validation (`required`, `pattern`, or a shared Zod schema) is for UX only — the server check is the real one.
- For large or complex forms, React Hook Form with the Zod resolver is acceptable if the project already uses it.

## Route Handlers instead of actions

Use `app/api/.../route.ts` only when something other than this React app calls the endpoint (webhooks, mobile clients, third parties). Apply the same validation and authorization rules.
