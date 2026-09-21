---
name: dotnet-security-baseline
description: Minimum ASP.NET Core security configuration for .NET 10 and the review baseline that goes with it — authentication, default-deny authorization, CORS, secrets, Data Protection, input validation, safe ProblemDetails, rate limiting, security headers, and vulnerability scanning. Use when designing or reviewing anything touching authn/authz, secrets, CORS, validation, or error output.
when_to_use:
  - Phase 3 (Plan) — deciding the authn/authz posture for each new endpoint in `03-design.md`.
  - Phase 4 (Build) — wiring an endpoint group, a validator, a rate-limit policy, or a secret.
  - Phase 7 (Code review) — walking the review baseline during `/net-review`.
  - Any time a diff adds an endpoint, a CORS origin, a config key, or an exception handler.
authoritative_references:
  - https://learn.microsoft.com/en-us/aspnet/core/security/
  - https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies
  - https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit
  - https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview
  - https://owasp.org/Top10/
  - .claude/skills/dotnet-10-conventions/SKILL.md
---

# .NET Security Baseline

## What every endpoint must declare

In `03-design.md`, for each new or changed endpoint, record all five. If any is unclear, write a
`Q-NNN` — do not pick a default.

- **AuthN** — anonymous, JWT bearer, OIDC cookie, or mTLS?
- **AuthZ** — which named policy? Which scope/role/claim does it require?
- **Input validation** — the FluentValidation rules, including length caps on every string.
- **Output** — does the response contain a field this caller is not entitled to see?
- **Audit** — should this action emit a structured `Information` log with `actor`, `subject`,
  `outcome`?

## Authentication

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.Authority = builder.Configuration["Auth:Authority"]!;
        o.Audience  = builder.Configuration["Auth:Audience"]!;
        o.TokenValidationParameters = new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        o.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    });
```

For a browser-facing app, use OIDC with a cookie handler instead:
`.AddOpenIdConnect(o => { o.ResponseType = "code"; o.UsePkce = true; o.SaveTokens = false; ... })`.
Authorization code + PKCE only — never implicit flow.

Never set `ValidateIssuer = false`, `ValidateAudience = false`, or a `ClockSkew` over a minute.

## Authorization: default-deny, per group

Every endpoint has an authorization decision. Apply it at the group so a new endpoint inherits
it rather than defaulting to open.

```csharp
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy("orders:read",  p => p.RequireClaim("scope", "orders:read"))
    .AddPolicy("orders:write", p => p.RequireClaim("scope", "orders:write"));

var orders = app.MapGroup("/api/orders")
    .RequireAuthorization("orders:read")
    .AddEndpointFilter<ValidationFilter>();

orders.MapPost("/", CreateOrder.Handle).RequireAuthorization("orders:write");

// Deliberate exception: public price lookup, no customer data in the response.
// Approved in ADR-0007. Rate-limited by the "public" policy.
orders.MapGet("/quote", Quote.Handle).AllowAnonymous().RequireRateLimiting("public");
```

`SetFallbackPolicy` plus `RequireAuthorization()` on each group is the default-deny mechanism.
`AllowAnonymous()` is a deliberate, justified exception: it carries a comment naming the reason
and an ADR, and the reviewer verifies the response body carries no user-scoped data.

Never authorize on a caller-supplied identifier alone. Re-resolve the resource scoped to the
authenticated subject: `_db.Orders.SingleOrDefaultAsync(o => o.Id == id && o.CustomerId == sub)`.

## Antiforgery

Stateless bearer-token APIs do not need antiforgery. **Cookie-authenticated form posts do.**

```csharp
builder.Services.AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN");
app.UseAntiforgery();

// Minimal API form binding validates the token automatically;
// opt out only with an explicit, justified reason:
app.MapPost("/webhooks/stripe", Stripe.Handle).DisableAntiforgery(); // signature-verified, no cookie auth
```

If the app has any cookie scheme, antiforgery is on. `DisableAntiforgery()` needs a comment.

## CORS

```csharp
var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
              ?? throw new InvalidOperationException("Cors:AllowedOrigins is required");

builder.Services.AddCors(o => o.AddPolicy("spa", p => p
    .WithOrigins(origins)               // explicit allowlist from configuration
    .WithMethods("GET", "POST", "PUT", "DELETE")
    .WithHeaders("Authorization", "Content-Type", "X-CSRF-TOKEN")
    .AllowCredentials()));
```

Origins come from configuration per environment, never from a literal in `Program.cs` and never
from a wildcard. `AllowAnyOrigin()` combined with `AllowCredentials()` is invalid in ASP.NET Core
and is a security hole wherever it is emulated (a reflected `Origin` header, a
`SetIsOriginAllowed(_ => true)`).

## Secrets

| Environment | Where secrets live |
|---|---|
| Local dev | `dotnet user-secrets` (`UserSecretsId` in the `.csproj`) — outside the repo tree. |
| Deployed | Environment variables injected by the orchestrator, or a vault provider (`AddAzureKeyVault`, `AddAmazonSecretsManager`). |
| Tests | Testcontainers-generated throwaway credentials. Never a shared account. |

```bash
dotnet user-secrets init --project src/Shop.Api
dotnet user-secrets set "ConnectionStrings:ShopDb" "Host=localhost;...;Password=devonly"
```

`appsettings.json` and `appsettings.<Env>.json` are committed and therefore contain **no**
secrets — only placeholders and non-sensitive defaults. Fail fast on a missing secret at
startup with a validated options class (`ValidateOnStart()`), never a silent default.

A committed `.env`, `appsettings.Production.json` with a real value, or any connection string
carrying a password is a **blocker** — and, once pushed, a credential rotation, not just a revert.

## Data Protection

Default Data Protection keys are stored per-instance in a local folder, so a multi-instance
deployment cannot decrypt another instance's cookies, antiforgery tokens, or protected payloads
— users see random 400s and forced logouts. Persist and encrypt the key ring:

```csharp
builder.Services.AddDataProtection()
    .SetApplicationName("shop")                       // must match across instances
    .PersistKeysToDbContext<ShopDbContext>()          // or blob storage / Redis
    .ProtectKeysWithAzureKeyVault(keyUri, credential);
```

Any deployment with a replica count above 1 and no persisted key ring is a blocker.

## Input validation is the outer boundary

Every request DTO has a FluentValidation validator, wired through an endpoint filter so
validation happens before the handler runs.

```csharp
public sealed record CreateOrderRequest(Guid CustomerId, string Reference, IReadOnlyList<OrderLineDto> Lines);

public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Reference).NotEmpty().MaximumLength(32)
            .Matches("^[A-Z0-9-]+$");                       // allowlist, not denylist
        RuleFor(x => x.Lines).NotEmpty().Must(l => l.Count <= 100)
            .WithMessage("An order may contain at most 100 lines.");
        RuleForEach(x => x.Lines).SetValidator(new OrderLineDtoValidator());
    }
}
```

Rules:

- **Every string has a `MaximumLength`.** An uncapped string is an unbounded allocation and an
  unbounded database write.
- **Every collection has a count cap.** Same reason.
- **Allowlist over denylist** — `Matches("^[A-Z0-9-]+$")`, not "reject `<script>`". Denylists
  are always incomplete.
- **Page sizes have a hard ceiling** enforced server-side regardless of what the caller sends.
- Boundary validation is not the whole story: state-dependent invariants ("order not already
  shipped") are checked in the handler against the loaded entity.

## Output safety

A ProblemDetails response tells the client *what* failed, never *how the server is built*.

```csharp
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    AllowStatusCode404Response = true,
    ExceptionHandlingPath = "/error"
});

app.Map("/error", (HttpContext ctx, ILogger<Program> logger) =>
{
    var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    var traceId = Activity.Current?.TraceId.ToString() ?? ctx.TraceIdentifier;

    ErrorLog.Unhandled(logger, ex!, traceId);          // full detail goes to the log

    return TypedResults.Problem(
        title: "An unexpected error occurred.",        // generic, stable text
        statusCode: StatusCodes.Status500InternalServerError,
        extensions: new Dictionary<string, object?> { ["traceId"] = traceId });
});
```

`ex.Message`, `ex.ToString()`, a stack trace, a SQL statement, a file path, or a type name must
never reach a client. Log the detail with the trace id; return the trace id. Support correlates
the two.

`app.UseDeveloperExceptionPage()` only inside `IsDevelopment()`.

## Rate limiting

```csharp
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddFixedWindowLimiter("public", l =>
    {
        l.PermitLimit = 30; l.Window = TimeSpan.FromMinutes(1); l.QueueLimit = 0;
    });
    o.AddTokenBucketLimiter("authenticated", l =>
    {
        l.TokenLimit = 200; l.TokensPerPeriod = 50;
        l.ReplenishmentPeriod = TimeSpan.FromSeconds(10); l.QueueLimit = 10;
    });
});
app.UseRateLimiter();

var orders = app.MapGroup("/api/orders").RequireRateLimiting("authenticated");
```

Every endpoint group carries a policy. Anonymous and authentication endpoints get the tighter
fixed window; authenticated business endpoints get the token bucket. A `QueueLimit` above zero
on a public policy is a queue an attacker can fill — keep it at 0 there.

## Security headers and HTTPS

```csharp
app.UseHsts();                 // non-development only
app.UseHttpsRedirection();

app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";  // API default
    h["X-Content-Type-Options"] = "nosniff";
    h["Referrer-Policy"] = "strict-origin-when-cross-origin";
    h["X-Frame-Options"] = "DENY";
    h.Remove("Server");
    await next();
});
```

For a browser-facing app the CSP is stricter about what it *allows*, not looser: named script
sources or a nonce, no `unsafe-inline`, no `unsafe-eval`. HSTS with `IncludeSubDomains` and a
max-age of at least one year once the domain is confirmed HTTPS-only.

## Dependency vulnerability scanning

```bash
dotnet list package --vulnerable --include-transitive
dotnet list package --deprecated
```

Run both in CI. Any `High` or `Critical` advisory fails the build; the fix is a version bump in
`Directory.Packages.props`. A CVE that cannot be fixed immediately gets one waiver with an ADR
naming the compensating control and an expiry date — not a permanent suppression.

## Review baseline (walk this during `/net-review`)

| # | Check | Severity if violated |
|---|---|---|
| 1 | Every new endpoint has a recorded authn + authz decision (group policy or justified `AllowAnonymous`). | blocker |
| 2 | `SetFallbackPolicy` requiring an authenticated user is configured. | blocker |
| 3 | No `AllowAnonymous()` without a comment and an ADR reference. | blocker |
| 4 | CORS origins come from configuration; no wildcard, no reflected `Origin`, no `AllowAnyOrigin` with credentials. | blocker |
| 5 | No secret, password, token, or connection-string credential in any committed file. | blocker |
| 6 | Data Protection keys persisted and encrypted if replicas > 1. | blocker |
| 7 | Every request DTO has a validator; every string has a length cap; every collection has a count cap. | major |
| 8 | Page size has a server-side ceiling. | major |
| 9 | No exception message, stack trace, SQL, or path in any response body. | blocker |
| 10 | Unhandled errors return generic ProblemDetails plus a `traceId`, and log the detail. | major |
| 11 | Every endpoint group has a rate-limit policy. | major |
| 12 | HSTS, CSP, `nosniff`, `Referrer-Policy`, HTTPS redirection present outside development. | major |
| 13 | All EF Core / ADO access is parameterised; no interpolated SQL outside `FromSql`/`ExecuteSql` interpolated-string overloads. | blocker |
| 14 | No hand-rolled crypto, hashing, or token format. | blocker |
| 15 | Authorization re-resolves the resource against the authenticated subject, not the raw route id. | blocker |
| 16 | `dotnet list package --vulnerable` reports no new High/Critical, or one ADR-backed waiver per advisory. | major |
| 17 | Antiforgery enabled wherever a cookie scheme exists; each `DisableAntiforgery()` justified. | major |
| 18 | Security-relevant events logged with `actor`/`action`/`outcome` and no PII. | minor |

## Forbidden

- `AllowAnyOrigin()` together with `AllowCredentials()`, or any equivalent
  `SetIsOriginAllowed(_ => true)` / reflected-`Origin` construction.
- A secret, token, or connection string containing a password committed to **any** file —
  `appsettings*.json`, `.env`, a compose file, a test fixture, a comment.
- Returning `ex.Message`, `ex.ToString()`, a stack trace, generated SQL, or a server file path
  to a client.
- An endpoint with no authorization decision recorded — no group policy, no
  `RequireAuthorization`, no justified `AllowAnonymous`.
- `[AllowAnonymous]` / `.AllowAnonymous()` without a comment stating why and an ADR.
- Disabling `UseHttpsRedirection` or `UseHsts` outside local development.
- `ValidateIssuer = false`, `ValidateAudience = false`, `ValidateLifetime = false`, or
  `RequireHttpsMetadata = false` outside development.
- Rolling your own crypto, password hashing, HMAC scheme, or token format. Use ASP.NET Core
  Identity's hasher, Data Protection, and the platform JWT handler.
- SQL built by string concatenation or `$"..."` passed to a non-interpolated `FromSqlRaw`
  overload.
- Relying on default Data Protection key storage in a multi-instance deployment.
