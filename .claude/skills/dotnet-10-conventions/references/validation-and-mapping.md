# Validation And Mapping

## Where validation runs

Validation runs in an **endpoint filter**, between model binding and the handler. By the time `HandleAsync` is entered, the request is valid. Handlers contain zero `if (string.IsNullOrWhiteSpace(...)) return Error(...)`.

## The validator

One validator per request DTO, living in the slice folder, named `<Request>Validator`.

```csharp
namespace Ordering.Features.PlaceOrder;

internal sealed class PlaceOrderValidator : AbstractValidator<PlaceOrderRequest>
{
    public PlaceOrderValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(x => x.Lines)
            .NotEmpty()
            .Must(lines => lines.Count <= 100)
            .WithMessage("An order may contain at most 100 lines.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.Sku).NotEmpty().Matches("^[A-Z0-9-]{3,32}$");
            line.RuleFor(l => l.Quantity).InclusiveBetween(1, 999);
            line.RuleFor(l => l.UnitPrice).GreaterThan(0m);
        });
    }
}
```

Rules:

- Validators are **synchronous** and pure. No `DbContext`, no HTTP call, no `MustAsync` hitting the database — uniqueness checks belong in the handler where they can be enforced inside the transaction.
- Every rule gets a message a client can act on. No default `'Lines' must not be empty.` for a business rule.
- Every rule needs a test. A rule with no `[Trait("AC", ...)]`-tagged test is untested behaviour.

## Registration

```csharp
services.AddValidatorsFromAssemblyContaining<PlaceOrderValidator>(ServiceLifetime.Singleton);
```

Validators are stateless — register them as singletons. Scan per module assembly, not per validator.

## The endpoint filter

One generic filter, defined once in the shared project, applied per endpoint.

```csharp
namespace Shared.Validation;

public sealed class ValidationFilter<TRequest>(IValidator<TRequest> validator)
    : IEndpointFilter
    where TRequest : notnull
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();

        if (request is null)
        {
            return TypedResults.Problem(
                title: "Malformed request body.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);

        if (!result.IsValid)
        {
            return TypedResults.ValidationProblem(result.ToDictionary());
        }

        return await next(context);
    }
}
```

Applied on the endpoint, not the group — only endpoints with a body need it:

```csharp
group.MapPost("/orders", HandleAsync)
     .AddEndpointFilter<ValidationFilter<PlaceOrderRequest>>()
     .ProducesValidationProblem();
```

`result.ToDictionary()` produces `IDictionary<string, string[]>` keyed by property path, which `TypedResults.ValidationProblem` renders as RFC 9457 `errors`. Snapshot-approve that payload with `Verify.XUnit` so the shape cannot drift.

## Filter ordering

Filters run in registration order, outermost first. Order on an endpoint:

1. Idempotency / request-dedup filter (if any).
2. `ValidationFilter<TRequest>`.
3. Any slice-specific filter (e.g. tenant resolution).

A filter that needs a *valid* request must be registered after the validation filter.

## Mapping with Mapster

Mapping is configuration, not code. One `IRegister` per module.

```csharp
namespace Ordering;

internal sealed class OrderingMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Order, GetOrderResponse>()
            .Map(dest => dest.TotalCents, src => (long)(src.Total * 100m))
            .Map(dest => dest.PlacedAt, src => src.PlacedAt)
            .Ignore(dest => dest.Links!);

        config.NewConfig<OrderLine, OrderLineResponse>();

        config.Default.RequireDestinationMemberSource(true);
    }
}
```

`RequireDestinationMemberSource(true)` makes an unmapped destination member a **configuration error** rather than a silent default value. Keep it on.

Registration in the module:

```csharp
var typeAdapterConfig = TypeAdapterConfig.GlobalSettings;
typeAdapterConfig.Scan(typeof(OrderingMappingConfig).Assembly);
typeAdapterConfig.Compile();                 // fail fast on a bad mapping
services.AddSingleton(typeAdapterConfig);
services.AddScoped<IMapper, ServiceMapper>();
```

`Compile()` at startup turns a broken mapping into a startup failure instead of a first-request 500. A test asserting `TypeAdapterConfig.GlobalSettings.Compile()` does not throw is mandatory.

## Mapping usage

```csharp
internal sealed class GetOrderHandler(OrderingDbContext db, IMapper mapper)
{
    public async Task<GetOrderResponse?> HandleAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Orders
            .AsNoTracking()
            .Where(o => o.Id == id)
            .ProjectToType<GetOrderResponse>(mapper.Config)
            .FirstOrDefaultAsync(cancellationToken);
}
```

`ProjectToType` pushes the mapping into SQL — only the columns the DTO needs are selected. Prefer it over loading an entity and calling `Adapt<T>()`.

| Situation | Use |
|---|---|
| Entity → response DTO from a query | `ProjectToType<T>(mapper.Config)` in the LINQ chain |
| Already-loaded object → DTO | `mapper.Map<T>(source)` |
| Request DTO → new entity | Hand-written factory (`Order.Create(...)`) — never Mapster |
| Request DTO → existing entity patch | Explicit property assignment inside the aggregate |

Mapster maps **outbound** only. Constructing or mutating a domain entity is domain logic and stays hand-written, so invariants run.

## Forbidden here

- Validation logic inside a handler that the validator should own.
- `MustAsync` / `CustomAsync` performing I/O in a validator.
- A `DbContext` or `HttpClient` injected into a validator.
- AutoMapper, or a hand-written `ToResponse()` extension when a Mapster config would do.
- Mapster used to build or mutate an entity from a request DTO.
- `Adapt<T>()` on a loaded entity when `ProjectToType<T>` would avoid loading it.
- `RequireDestinationMemberSource(false)` or removing the startup `Compile()`.
- Validation rules without a matching `[Trait("AC", ...)]` test.
