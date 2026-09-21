---
name: dotnet-observability-ops
description: Observability and operations for .NET 10 services — LoggerMessage source-generated logging, OpenTelemetry tracing and metrics, liveness/readiness HealthChecks, Aspire AppHost local composition, Docker packaging, and measurement-first performance work. Use when adding logging, metrics, traces, health endpoints, container packaging, or when asked why something is slow.
when_to_use:
  - Phase 3 (Plan) — deciding what a new slice must emit (logs, spans, metrics) and what its SLO is.
  - Phase 4 (Build) — wiring `ILogger<T>`, an `ActivitySource`, a `Meter`, or a health check for a slice.
  - Phase 7 (Code review) — flagging unstructured logging, PII in logs, missing instrumentation, or a container running as root.
  - On-demand — user asks "why is this slow?", "how do we see this in prod?", or "is this container image right?".
authoritative_references:
  - https://learn.microsoft.com/en-us/dotnet/core/extensions/logger-message-generator
  - https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing
  - https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks
  - https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/app-host-overview
  - https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-counters
  - .claude/skills/dotnet-10-conventions/SKILL.md
---

# .NET Observability and Operations

## Structured logging with LoggerMessage

Every log call goes through a source-generated `partial` method. `ILogger<T>` is injected;
the message template is declared once, at compile time.

```csharp
internal static partial class OrderLog
{
    [LoggerMessage(Level = LogLevel.Information,
        Message = "Order {OrderId} accepted for customer {CustomerId} with {LineCount} lines")]
    public static partial void OrderAccepted(ILogger logger, Guid orderId, Guid customerId, int lineCount);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Order {OrderId} rejected: {Reason}")]
    public static partial void OrderRejected(ILogger logger, Guid orderId, string reason);
}

OrderLog.OrderAccepted(logger, order.Id, order.CustomerId, order.Lines.Count);
```

Why this beats `logger.LogInformation($"Order {id} accepted")`:

| Interpolated string | LoggerMessage |
|---|---|
| Formats and allocates the string **before** the level check — cost paid even when disabled. | Generated code checks `IsEnabled` first; zero allocation when the level is off. |
| Emits one opaque text field. | Emits `OrderId`, `CustomerId`, `LineCount` as queryable structured fields. |
| Boxes every value type argument. | Strongly-typed parameters, no boxing. |
| Template drifts per call site. | Template declared once; every call site is consistent. |

A `$"..."` inside any `Log*` call is a review blocker. So is `string.Format`, `+` concatenation,
and `logger.LogInformation("...", args)` with a positional template written ad hoc in a hot path.

### Levels

| Level | Belongs here | Never here |
|---|---|---|
| `Trace` | Per-iteration detail, wire payload shapes. Off outside local dev. | Anything in a deployed default config. |
| `Debug` | Decision points inside a handler, cache hit/miss. | Per-request noise in production. |
| `Information` | Business events that a human would want in an audit trail: order accepted, payment captured. | Per-field validation detail; loop bodies. |
| `Warning` | A recoverable anomaly: retry exhausted then fallback, expected-but-unusual input, deprecated endpoint hit. | Ordinary validation failures (those are a 400, not a warning). |
| `Error` | An operation failed and the caller is affected. Include the exception as the first argument. | Anything you then rethrow — log once, at the boundary. |
| `Critical` | The process cannot continue or a dependency is wholly unavailable. | Anything recoverable. |

### Never log

Secrets, tokens, full JWTs, passwords, connection strings, card numbers, email addresses,
names, addresses, or any whole request/entity object. `logger.LogInformation("{@Request}", req)`
is a blocker — it serializes whatever fields the DTO grows next year.

Log identifiers, not payloads. Mask when a prefix is genuinely needed: `code=ABC***`.

### Correlation

Correlation is ambient, not a parameter you thread. The current span carries it:

```csharp
Activity.Current?.SetTag("order.id", order.Id);
var correlationId = Activity.Current?.TraceId.ToString();
```

The OpenTelemetry logging provider attaches `TraceId` and `SpanId` to every record
automatically — do not add your own `CorrelationId` parameter to a `[LoggerMessage]`.

## OpenTelemetry wiring

```csharp
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("ordering-api"))
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation(o => o.RecordException = true)
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddSource(OrderingTelemetry.ActivitySourceName)
        .AddOtlpExporter())
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter(OrderingTelemetry.MeterName)
        .AddOtlpExporter());

builder.Logging.AddOpenTelemetry(o => { o.IncludeScopes = true; o.IncludeFormattedMessage = true; });
```

One `ActivitySource` and one `Meter` per module, declared as static singletons — never
constructed per request:

```csharp
public static class OrderingTelemetry
{
    public const string ActivitySourceName = "Shop.Ordering";
    public const string MeterName = "Shop.Ordering";

    public static readonly ActivitySource Source = new(ActivitySourceName);
    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> OrdersAccepted =
        Meter.CreateCounter<long>("shop.ordering.orders.accepted", unit: "{order}");
    public static readonly Histogram<double> PricingDuration =
        Meter.CreateHistogram<double>("shop.ordering.pricing.duration", unit: "s");
}
```

### Metric naming

`<product>.<module>.<subject>.<unit-or-verb>`, dot-separated, lowercase, singular subject,
plural only for counted things. Units follow the OTel convention: seconds (`s`) not
milliseconds, bytes (`By`), dimensionless counts as `{thing}`. Never bake a dimension into
the name — `orders.accepted` with a `tier` tag, not `orders.accepted.gold`. Keep tag
cardinality bounded: no order ids, user ids, or raw paths as tag values.

### Every service must expose

- HTTP server request duration and count (from `AddAspNetCoreInstrumentation`).
- Outbound HTTP client duration and count, tagged by logical client name.
- .NET runtime metrics: GC, thread pool queue length, exception count
  (`AddRuntimeInstrumentation`).
- EF Core / DB command duration.
- One domain counter and one domain histogram per slice with an SLO — the business event and
  the latency of the operation that produces it.

## HealthChecks: liveness and readiness are different questions

```csharp
builder.Services.AddHealthChecks()
    .AddNpgSql(cs, name: "postgres", tags: ["ready"])
    .AddUrlGroup(paymentsUri, name: "payments", tags: ["ready"])
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);

app.MapHealthChecks("/health/live",  new() { Predicate = r => r.Tags.Contains("live")  });
app.MapHealthChecks("/health/ready", new() { Predicate = r => r.Tags.Contains("ready") });
```

**Liveness answers "is this process wedged?"** — it must not touch a dependency. A liveness
check that probes Postgres will kill every replica of your service during a database blip,
turning a degradation into an outage.

**Readiness answers "should this instance receive traffic?"** — it includes every dependency
the instance needs to serve a request. A readiness failure removes one instance from the load
balancer, which is exactly the desired behavior.

Tag every check `live` or `ready`; a check with neither tag runs on no endpoint and is dead code.
Readiness checks get a timeout, and both endpoints are `AllowAnonymous` but not exposed publicly
through the ingress.

## Local composition: Aspire.Hosting.AppHost 13.x

Aspire composes the local development topology. It is a **dev and compose-time concern** —
the production runtime is a container in your orchestrator, not the AppHost.

```csharp
// src/Shop.AppHost/AppHost.cs
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .AddDatabase("shopdb");

var api = builder.AddProject<Projects.Shop_Api>("api")
    .WithReference(postgres)
    .WaitFor(postgres);

builder.AddNpmApp("frontend", "../shop-web", "start")
    .WithReference(api)
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints();

builder.Build().Run();
```

`WithReference` injects connection strings and service-discovery entries, so the API resolves
the frontend or a sibling service by logical name (`https+http://api`) rather than a hardcoded
port. The AppHost project ships no business logic, carries no ACs, and is never the deployed
artifact. Aspire's ServiceDefaults extension (OTel + health checks + resilience defaults) is
the one part that **does** run in production — reference it from the API project.

## Docker conventions

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/Shop.Api/Shop.Api.csproj src/Shop.Api/
RUN dotnet restore src/Shop.Api/Shop.Api.csproj
COPY . .
RUN dotnet publish src/Shop.Api/Shop.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["./Shop.Api"]
```

Rules:

- **Multi-stage, always.** The SDK exists only in the `build` stage.
- **Chiselled or distroless runtime image** (`-noble-chiseled`), not the full `aspnet` tag and
  never the SDK tag. No shell, no package manager, smallest CVE surface.
- **Non-root.** `USER $APP_UID` (the image's pre-created 64198 user). Port 8080, not 80 —
  a non-root process cannot bind a privileged port.
- **Copy `.csproj` and the central props files first**, restore, then copy the rest — restore
  layers cache on dependency changes, not source changes.
- **`.dockerignore`** excluding `bin/`, `obj/`, `artifacts/`, `.git/`, `**/*.user`,
  `tests/`, `.specs/`. Without it, `COPY . .` ships stale `obj/` and busts every cache layer.
- No secrets in `ENV` or build args. Configuration arrives at runtime.

## Performance: measure before optimising

> No optimisation without a measurement. "It feels slow" is a hypothesis, not a finding.

A perf-labelled change lands with a before/after artifact recorded in
`05-implementation-log.md`. Without it, request changes.

| Tool | Use for |
|---|---|
| `dotnet-counters monitor -p <pid> --counters System.Runtime,Microsoft.AspNetCore.Hosting` | First look: allocation rate, GC pauses, thread-pool queue depth, request rate. Near-zero overhead. |
| `dotnet-trace collect -p <pid> --profile cpu-sampling` | CPU profile; open the `.nettrace` in PerfView or Speedscope to find the hot stack. |
| `dotnet-counters` + `dotnet-gcdump` | Heap composition and LOH pressure when allocation rate is the suspect. |
| **BenchmarkDotNet** | Micro-benchmarks of a parser, serializer, comparer, or hot loop. `[MemoryDiagnoser]` on, ≥1 fork, and never in the unit test projects — a separate `benchmarks/` project. |
| `EXPLAIN (ANALYZE, BUFFERS)` | The actual query plan. EF Core's generated SQL comes from the OTel EF instrumentation or `LogTo`. |

### Common .NET antipatterns

- **Sync-over-async.** `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` on a request path —
  consumes a thread-pool thread and deadlocks under load. Make the whole path `async`.
- **`Task.Run` to fake async.** Wrapping synchronous work in `Task.Run` inside a request adds a
  thread hop and returns the same thread to the pool it took from. Net loss.
- **Allocation in hot paths.** `string.Format`, `string +` in loops, `ToList()` on an already-
  enumerable result, per-request `JsonSerializerOptions` (cache it as a static), closure capture
  in lambdas. Use `ArrayPool<T>`, `Span<T>`, `StringBuilder`, and source-generated JSON.
- **LINQ in tight loops.** `Where`/`Select`/`First` allocate enumerators and delegates per call.
  Fine at the boundary; a `for` loop inside a per-element hot path.
- **Large object heap pressure.** Any array or buffer ≥85 KB lands on the LOH, which is only
  compacted on demand. Chunk large reads; pool buffers; stream large responses instead of
  materialising them.
- **Missing `ConfigureAwait(false)` in library code.** In a class library, every `await` gets
  `.ConfigureAwait(false)` (or set `<ConfigureAwaitAnalyzer>`); ASP.NET Core app code does not
  need it, but shared libraries may be consumed by a synchronization-context host.
- **Unbounded concurrency.** `Task.WhenAll` over a user-supplied collection fans out without
  limit. Bound it with `Parallel.ForEachAsync(..., new ParallelOptions { MaxDegreeOfParallelism = n })`
  or a `SemaphoreSlim`. Same for unbounded `Channel<T>` — give it a capacity and a full mode.
- **No pagination cap.** An endpoint honouring a caller-supplied `pageSize` without a hard
  ceiling is a denial-of-service primitive.
- **Missing response caching and compression.** `AddOutputCache()` for cacheable GETs with an
  explicit expiry, and `AddResponseCompression()` (Brotli) for JSON over ~1 KiB.
- **No timeouts on outbound calls.** Every `IHttpClientFactory` client gets
  `AddStandardResilienceHandler()` — timeout, retry with jitter, and circuit breaker — and an
  explicit `Timeout`. Defaults are not a decision.

## Forbidden

- `Console.WriteLine` (or `Debug.WriteLine`, or `Trace.Write`) used as logging anywhere in
  `src/`.
- An interpolated string, `string.Format`, or `+` concatenation inside `LogInformation` or any
  other `Log*` call. Use a `[LoggerMessage]` partial.
- Logging a whole request, response, entity, or DTO object — `"{@Order}"`, `"{Request}"`,
  `JsonSerializer.Serialize(dto)` into a log message.
- Any secret, token, password, connection string, or PII field in a log record.
- A single `/health` endpoint serving both liveness and readiness, or a liveness check that
  touches a database, queue, or remote HTTP dependency.
- Running the container as root, or omitting `USER`; binding port 80 in a non-root image.
- An SDK image, or the un-chiselled `aspnet` image, as the final stage of a production
  Dockerfile.
- Shipping a repo with no `.dockerignore`.
- Optimising without a `dotnet-counters`/`dotnet-trace`/BenchmarkDotNet/`EXPLAIN` artifact
  recorded in `05-implementation-log.md`.
- `Task.Run` to fake async in a request path, and `.Result` / `.Wait()` /
  `.GetAwaiter().GetResult()` anywhere on one.
- A new `ActivitySource` or `Meter` constructed per request instead of a static singleton.
- Business logic in the Aspire AppHost project, or treating the AppHost as the production
  runtime.
