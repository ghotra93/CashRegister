# Minimal APIs — Groups, Slices, TypedResults, ProblemDetails, Versioning

## Module composition

Each module exposes exactly two extension methods. `Program.cs` calls them and nothing else.

```csharp
// src/Ordering/OrderingModule.cs
namespace Ordering;

public static class OrderingModule
{
    public static IServiceCollection AddOrderingModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContextPool<OrderingDbContext>(o =>
            o.UseNpgsql(configuration.GetConnectionString("Orders")));
        services.AddScoped<PlaceOrderHandler>();
        services.AddScoped<GetOrderHandler>();
        services.AddValidatorsFromAssemblyContaining<PlaceOrderValidator>();
        return services;
    }

    public static IEndpointRouteBuilder MapOrderingEndpoints(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("/orders")
                           .WithTags("Orders")
                           .RequireAuthorization();
        group.MapPlaceOrder();
        group.MapGetOrder();
        return builder;
    }
}
```

```csharp
// src/Api/Program.cs — composition only
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddOpenApi("v1");
builder.Services.AddApiVersioning(o =>
{
    o.DefaultApiVersion = new ApiVersion(1, 0);
    o.ReportApiVersions = true;
}).AddApiExplorer();

builder.Services.AddOrderingModule(builder.Configuration);
builder.Services.AddGiftCardsModule(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapOpenApi();
app.MapScalarApiReference();

var v1 = app.NewVersionedApi()
            .MapGroup("/api/v{version:apiVersion}")
            .HasApiVersion(1, 0);
v1.MapOrderingEndpoints();
v1.MapGiftCardsEndpoints();

app.Run();
```

Group metadata (`WithTags`, `RequireAuthorization`, filters, rate limiting) is set **once on the group**, never repeated per endpoint.

## TypedResults matrix

| Outcome | Return type | Call |
|---|---|---|
| 200 with body | `Ok<TResponse>` | `TypedResults.Ok(body)` |
| 201 with body + Location | `Created<TResponse>` | `TypedResults.Created(uri, body)` |
| 202 accepted | `Accepted<TResponse>` | `TypedResults.Accepted(uri, body)` |
| 204 no content | `NoContent` | `TypedResults.NoContent()` |
| 400 validation | `ValidationProblem` | `TypedResults.ValidationProblem(errors)` |
| 401 / 403 | `UnauthorizedHttpResult` / `ForbidHttpResult` | `TypedResults.Unauthorized()` |
| 404 | `NotFound` | `TypedResults.NotFound()` |
| 409 / 422 / other domain error | `ProblemHttpResult` | `TypedResults.Problem(...)` |
| Stream / file | `FileStreamHttpResult` | `TypedResults.Stream(stream, contentType)` |

Two or more possible outcomes → declare `Results<A, B>`:

```csharp
private static async Task<Results<Ok<GetOrderResponse>, NotFound>> HandleAsync(
    Guid id, GetOrderHandler handler, CancellationToken cancellationToken)
{
    var order = await handler.HandleAsync(id, cancellationToken);
    return order is null ? TypedResults.NotFound() : TypedResults.Ok(order);
}
```

`Results<,>` is what feeds OpenAPI response metadata. An untyped `IResult` produces an empty, useless schema — that is the real reason it is banned.

## ProblemDetails and IExceptionHandler

```csharp
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ctx =>
{
    ctx.ProblemDetails.Instance = $"{ctx.HttpContext.Request.Method} {ctx.HttpContext.Request.Path}";
    ctx.ProblemDetails.Extensions["traceId"] =
        Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier;
});
```

```csharp
internal sealed class DomainExceptionHandler(ILogger<DomainExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DomainException domain)
        {
            return false; // let the next handler (or the default 500) take it
        }

        logger.LogWarning(domain, "Domain rule violated: {Rule}", domain.Rule);

        await Results.Problem(
            type: $"https://errors.ordering.example/{domain.Rule}",
            title: domain.Rule,
            detail: domain.Message,
            statusCode: StatusCodes.Status409Conflict)
            .ExecuteAsync(httpContext);

        return true;
    }
}
```

Rules:

- `TryHandleAsync` returns `false` for exceptions it does not own. Handlers are registered in order; the last one may return `true` for everything and log at `Error`.
- The `type` URI is stable and documented. Clients switch on `type`, never on `title` or `detail`.
- Never put an exception message from an infrastructure failure into `detail`.

## API versioning

```csharp
var v1 = app.NewVersionedApi("Orders")
            .MapGroup("/api/v{version:apiVersion}")
            .HasApiVersion(1, 0);

var v2 = app.NewVersionedApi("Orders")
            .MapGroup("/api/v{version:apiVersion}")
            .HasApiVersion(2, 0);
```

- Breaking a response shape → new major version, new slice folder (`Features/PlaceOrderV2/`). Do not add optional fields to reshape an existing response.
- Deprecate with `.HasDeprecatedApiVersion(1, 0)`; `ReportApiVersions = true` surfaces it in the `api-supported-versions` header.

## The OpenAPI document is generated, not authored

The JVM side of this toolkit is **contract-first**: `openapi.yaml` is hand-authored, the diff gate runs against it, and server interfaces are generated from it.

The .NET side is **code-first**: the endpoint signatures (`Results<,>`, DTO records, `ProducesProblem`) *are* the contract, and the document is generated from them at build time.

```bash
# emitted by the harness; never hand-edited, always committed
dotnet build
dotnet run --project src/Api --getdocument:output artifacts/openapi/openapi.json
```

Consequences:

- Never hand-edit `artifacts/openapi/openapi.json`. Change the endpoint signature instead.
- The diff gate compares the newly generated document against the committed one and fails on a breaking change. A deliberate break needs a version bump plus an ADR.
- A missing `ProducesProblem` or an untyped `IResult` silently degrades the generated document — that is a review blocker, not a nit.
- Snapshot-approve the document in a test (`Verify.XUnit`) so an accidental shape change fails a unit test, not only the diff gate.

## Endpoint metadata checklist

Every endpoint carries, at minimum:

```csharp
group.MapPost("/orders", HandleAsync)
     .WithName("PlaceOrder")               // operationId
     .WithSummary("Places a new order.")
     .WithDescription("Creates an order for the authenticated customer.")
     .AddEndpointFilter<ValidationFilter<PlaceOrderRequest>>()
     .Produces<PlaceOrderResponse>(StatusCodes.Status201Created)
     .ProducesValidationProblem()
     .ProducesProblem(StatusCodes.Status409Conflict);
```

`WithName` must be unique across the app; it becomes the OpenAPI `operationId` and the argument to `LinkGenerator`.
