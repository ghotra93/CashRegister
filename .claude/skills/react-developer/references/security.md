# Security

## Secrets and environment variables

- Only variables prefixed `NEXT_PUBLIC_` are inlined into client bundles. Everything else is server-only — and must stay that way.
- Modules that read secrets, talk to the database, or call privileged APIs start with `import 'server-only'`.
- Never pass a secret, token, or full user record to a Client Component as a prop; pass only the fields the UI needs.
- `.env*.local` files are never committed.

## Server Actions and Route Handlers

- Treat each as a public HTTP endpoint: validate every input with a schema, authenticate, and authorize the specific resource (not just "is logged in").
- Never trust hidden form fields or client-sent IDs for authorization decisions.
- Rate-limit endpoints that are expensive or unauthenticated.
- Return safe error messages; log details server-side.

## Output

- React escapes text by default. `dangerouslySetInnerHTML` is allowed only with sanitized input (e.g. DOMPurify) and a comment explaining the source.
- Never build `href` values from user input without allowing only `http:`/`https:` (block `javascript:` URLs).
- Validate redirect targets against an allowlist to avoid open redirects.

## Headers

- Configure security headers in `next.config` `headers()` or middleware: `Content-Security-Policy` (nonce-based when inline scripts are needed), `X-Content-Type-Options: nosniff`, `Referrer-Policy`, `Permissions-Policy`, and `frame-ancestors` in the CSP.

## Dependencies

- Run the package manager's audit (`npm audit --audit-level=high`) in validation; no new dependency without user approval.
