# ADR-005: v1 security posture

- **Status:** accepted
- **Date:** 2026-09-21
- **Deciders:** user, `dotnet-architect`

## Context and problem statement

The user said no authentication in v1, with JWT in v2 (Q-012). The API accepts file uploads from a browser SPA.

## Decision outcome

- **Auth:** none. This overrides the security-baseline default of deny-by-default, as the user explicitly waived authentication for v1 (NFR-004).
- **CORS:** a named policy with origins taken from `Cors:AllowedOrigins`. Development allows only `http://localhost:5173`. There is no wildcard.
- **Upload size:** 1 MB request limit on the upload endpoint. 1000 lines fit comfortably within it.
- The file name is sanitised before it is echoed in `Content-Disposition`.
- **Errors:** ProblemDetails with no exception details outside Development.
- **Deferred:** rate limiting and security headers beyond the defaults. The app is internal and unauthenticated in v1; revisit together with JWT in v2.

### Consequences

- Negative: anyone who can reach the API can change the divisor. This is accepted for v1 and documented.

## Links

- Source: `01-spec.md` NFR-004, Q-012
