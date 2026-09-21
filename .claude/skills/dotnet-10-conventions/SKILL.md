---
name: dotnet-10-conventions
description: .NET 10 LTS / C# 14 / ASP.NET Core idioms and defaults for a modular monolith built from Minimal API vertical slices. Use when writing or reviewing endpoints, handlers, DTOs, options, HTTP clients, or anything touching the ASP.NET Core programming model.
when_to_use:
  - Phase 3 (Plan) — choosing the slice layout and endpoint surface for a feature.
  - Phase 4 (Build) — writing a new endpoint, handler, DTO, validator, or typed HTTP client.
  - Phase 7 (Code review) — bringing MVC-era or .NET 8-era patterns up to .NET 10 / C# 14.
  - Anywhere an error path returns something other than ProblemDetails.
authoritative_references:
  - https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/overview
  - https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14
  - https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi
  - https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience
---

# .NET 10 / C# 14 Conventions

> .NET 10 LTS + C# 14 + ASP.NET Core Minimal APIs. Prefer the most modern idiom unless an ADR explains otherwise.

## Solution Layout — Vertical Slices In A Modular Monolith

One project per module. Inside a module, one folder per slice. A slice owns its endpoint, its handler and its DTOs; nothing else may reach into it.

```
src/
├── Api/                        # host: Program.cs, DI composition, nothing else
│   └── Program.cs
├── Ordering/                     # module
│   ├── Contracts/                       # ONLY public surface of the module
│   │   ├── IOrderPricing.cs
│   │   └── OrderPlaced.cs
│   ├── Features/
│   │   ├── PlaceOrder/                  # one folder per slice
│   │   │   ├── PlaceOrderEndpoint.cs
│   │   │   ├── PlaceOrderHandler.cs
│   │   │   ├── PlaceOrderRequest.cs
│   │   │   ├── PlaceOrderResponse.cs
│   │   │   └── PlaceOrderValidator.cs
│   │   └── GetOrder/
│   │       ├── GetOrderEndpoint.cs
│   │       ├── GetOrderHandler.cs
│   │       └── GetOrderResponse.cs
│   ├── Persistence/
│   │   ├── OrderingDbContext.cs
│   │   └── Configurations/OrderConfiguration.cs
│   └── OrderingModule.cs                  # AddOrderingModule / MapOrderingEndpoints
└── GiftCards/                  # another module, same shape
```

Rules:

- A slice folder is the unit of change. Every type in it is `internal sealed` unless it is a DTO the endpoint binds.
- Cross-module access goes through `Contracts/` only. Never reference another module's `Features/` or `Persistence/` types.
- Cross-*slice* reuse is forbidden. If two slices need the same logic, promote it to the module root (or `Contracts/`) and reference it from both — do not reference slice-to-slice.
- `Program.cs` composes modules. It contains no business logic.

## Endpoints

Every slice exposes one static `MapX` method. Endpoints are registered into a versioned `MapGroup`.

```csharp
namespace Ordering.Features.PlaceOrder;

internal static class PlaceOrderEndpoint
{
    internal static RouteGroupBuilder MapPlaceOrder(this RouteGroupBuilder group)
    {
        group.MapPost("/orders", HandleAsync)
             .WithName("PlaceOrder")
             .WithSummary("Places a new order.")
             .AddEndpointFilter<ValidationFilter<PlaceOrderRequest>>()
             .ProducesProblem(StatusCodes.Status400BadRequest);
        return group;
    }

    private static async Task<Results<Created<PlaceOrderResponse>, ProblemHttpResult>> HandleAsync(
        PlaceOrderRequest request,
        PlaceOrderHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request, cancellationToken);
        return result.IsSuccess
            ? TypedResults.Created($"/api/v1/orders/{result.Value.Id}", result.Value)
            : TypedResults.Problem(title: result.Error, statusCode: StatusCodes.Status409Conflict);
    }
}
```

- Return `TypedResults.*` wrapped in `Results<T1, T2>` (or `Ok<T>`, `Created<T>`, `NoContent`). Never bare `Results.` and never an untyped `IResult`.
- The endpoint delegate is a thin adapter: bind, call handler, map to a status. No business logic, no `DbContext`.
- The handler is `internal sealed`, takes a primary constructor, returns a domain result — never an `IResult`.

| Situation | Use |
|---|---|
| Created a resource | `TypedResults.Created<T>(uri, body)` |
| Read succeeded | `TypedResults.Ok<T>(body)` |
| Delete / update with no body | `TypedResults.NoContent()` |
| Validation failed | `TypedResults.ValidationProblem(errors)` |
| Expected business failure | `TypedResults.Problem(...)` with a stable `type` URI |
| Not found | `TypedResults.NotFound()` (ProblemDetails body via `AddProblemDetails`) |
| Two or more possible statuses | `Results<Ok<T>, NotFound>` as the return type |

- **Minimal APIs and endpoint groups**: slice layout, `MapGroup` composition, the full `TypedResults` matrix, ProblemDetails + `IExceptionHandler` wiring, API versioning, and how the OpenAPI document is generated. Read [minimal-apis.md](references/minimal-apis.md)

## Errors — ProblemDetails On Every Path

```csharp
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = ctx =>
        ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);

builder.Services.AddExceptionHandler<DomainExceptionHandler>();
// ...
app.UseExceptionHandler();
app.UseStatusCodePages();
```

- Expected failures are **return values** (a `Result<T>` or a discriminated return type), not exceptions.
- Unexpected failures are translated by an `IExceptionHandler` into ProblemDetails. One handler per exception family.
- Never leak a stack trace, a SQL fragment, or an inner exception message into the response body.

## API Versioning And OpenAPI

```csharp
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
}).AddApiExplorer();

builder.Services.AddOpenApi("v1");
// ...
app.MapOpenApi();
app.MapScalarApiReference();

var v1 = app.NewVersionedApi("Orders").MapGroup("/api/v{version:apiVersion}").HasApiVersion(1, 0);
v1.MapOrderingEndpoints();
```

- `Asp.Versioning.Http` for versioning; the version lives in the route, never in a header only.
- `Microsoft.AspNetCore.OpenApi` (`AddOpenApi` / `MapOpenApi`) generates the document. **Scalar** (`MapScalarApiReference`) is the UI — not Swagger UI.
- The OpenAPI document is **generated at build time** into `artifacts/openapi/openapi.json`. It is never hand-authored.

## Validation And Mapping

- FluentValidation runs in an **endpoint filter**, before the handler. Handlers assume valid input.
- Mapster does DTO mapping with a registered `IRegister` config. No hand-written mapper classes, no AutoMapper.

- **Validation and mapping**: FluentValidation validator + reusable endpoint filter, Mapster `IRegister` config and compile-time mapping. Read [validation-and-mapping.md](references/validation-and-mapping.md)

## Outbound HTTP

```csharp
builder.Services.AddHttpClient<GiftCardClient>(client =>
{
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
}).AddStandardResilienceHandler();
```

- Typed clients via `IHttpClientFactory`. One client class per external API, `internal sealed`, primary constructor taking `HttpClient`.
- `Microsoft.Extensions.Http.Resilience` `AddStandardResilienceHandler()` on every outbound client. Tune, do not hand-roll.

- **HTTP and resilience**: typed clients, standard resilience handler, retry/timeout/circuit-breaker tuning, Polly strategies. Read [http-and-resilience.md](references/http-and-resilience.md)

## Configuration

```csharp
builder.Services.AddOptions<GiftCardOptions>()
    .Bind(builder.Configuration.GetSection("GiftCards"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

```csharp
internal sealed class GiftCardOptions
{
    [Required, Url]
    public required string BaseUrl { get; init; }

    [Range(1, 60)]
    public int TimeoutSeconds { get; init; } = 10;
}
```

- One options class per module, `ValidateOnStart()` always. A misconfigured app must fail at startup, not at first request.
- Never inject `IConfiguration` into a handler. Inject `IOptions<T>` (or the value via primary constructor).

## C# 14 Defaults

| Concern | Default |
|---|---|
| Namespaces | File-scoped (`namespace X;`) |
| Dependencies | Primary constructors |
| DTOs | `record` / `record struct`, `required` members |
| Classes | `sealed` unless designed for inheritance |
| Visibility | `internal` unless in `Contracts/` |
| Collections | Collection expressions (`[]`, `[..a, b]`) |
| Nullability | `<Nullable>enable</Nullable>`, no `!` without a comment |
| Time | Injected `TimeProvider`, never `DateTime.Now`/`UtcNow` |

```csharp
namespace Ordering.Features.PlaceOrder;

internal sealed record PlaceOrderRequest
{
    public required string CustomerId { get; init; }
    public required IReadOnlyList<OrderLine> Lines { get; init; }
}

internal sealed class PlaceOrderHandler(
    OrderingDbContext db,
    IOrderPricing pricing,
    TimeProvider timeProvider)
{
    public async Task<Result<PlaceOrderResponse>> HandleAsync(
        PlaceOrderRequest request,
        CancellationToken cancellationToken)
    {
        var total = pricing.Total(request.Lines);
        var order = Order.Create(request.CustomerId, total, timeProvider.GetUtcNow());
        db.Orders.Add(order);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(new PlaceOrderResponse(order.Id, total));
    }
}
```

## Async

- Async all the way. Every I/O-bound method is `async Task` / `async Task<T>`.
- `CancellationToken` is a parameter on every handler, every EF Core call, every `HttpClient` call — threaded from the endpoint, never `CancellationToken.None`.
- `ConfigureAwait` is unnecessary in ASP.NET Core; omit it.

## Forbidden

- MVC controllers (`ControllerBase`, `[ApiController]`) for new slices.
- `Results.Ok()` / any untyped `IResult` return type.
- Throwing a raw exception for an expected failure (not found, conflict, validation).
- Business logic in `Program.cs` or in an endpoint delegate.
- `async void`.
- `.Result`, `.Wait()`, `GetAwaiter().GetResult()` — anywhere.
- `new HttpClient(...)`.
- `IServiceProvider.GetService`/`GetRequiredService` inside a handler (service locator).
- A slice referencing another slice's types, or a module referencing another module's non-`Contracts` types.
- `catch (Exception)` that swallows or rethrows with no context.
- `IConfiguration` injected into a handler; `[Required]`-less options; options without `ValidateOnStart()`.
- AutoMapper, Swashbuckle, Newtonsoft.Json in new code.
