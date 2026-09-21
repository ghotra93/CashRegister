---
name: efcore-10-data-access
description: EF Core 10 data-access defaults for a modular monolith — one pooled DbContext per module, configuration classes, migrations as an explicit reviewed pipeline step, no-tracking projections, mandatory pagination, and concurrency tokens. Use when adding or reviewing an entity, a query, a migration, or a DbContext registration.
when_to_use:
  - Phase 3 (Plan) — designing the schema delta and the migration for a feature.
  - Phase 4 (Build) — writing a query, an entity configuration, or a handler that saves.
  - Phase 6 (Validate) — when a query shows up as slow or a migration fails to apply.
  - Phase 7 (Code review) — N+1 queries, unbounded results, missing `AsNoTracking`, startup migration.
authoritative_references:
  - https://learn.microsoft.com/en-us/ef/core/
  - https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying
  - https://learn.microsoft.com/en-us/ef/core/querying/tracking
  - https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying
---

# EF Core 10 Data Access

> One `DbContext` per module, pooled. Reads are projections. Writes are one `SaveChangesAsync` per request. Schema changes are reviewed migrations applied by a script, never by the app.

## DbContext Per Module

```csharp
namespace Ordering.Persistence;

public sealed class OrderingDbContext(DbContextOptions<OrderingDbContext> options)
    : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("ordering");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderingDbContext).Assembly);
    }
}
```

```csharp
services.AddDbContextPool<OrderingDbContext>(options => options
    .UseNpgsql(configuration.GetConnectionString("Ordering"),
        npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "ordering"))
    .UseSnakeCaseNamingConvention());
```

- `AddDbContextPool` reuses context instances and is the default. It forbids constructor injection of scoped services into the context — that is a feature, not a limitation: a `DbContext` holds no collaborators.
- Each module owns its own schema (`ordering`, `giftcards`) and its own migrations history table, so migrations do not collide.
- `OnModelCreating` only sets the default schema and calls `ApplyConfigurationsFromAssembly`. No entity-by-entity configuration inline.

| Situation | Registration |
|---|---|
| Normal module context | `AddDbContextPool<T>` |
| Context needing a scoped service (interceptor with tenant state) | `AddDbContext<T>` plus an ADR explaining the pooling loss |
| Read-only reporting context | `AddDbContextPool<T>` with `UseQueryTrackingBehavior(NoTracking)` |
| Design-time (migrations) | `IDesignTimeDbContextFactory<T>` in the module project |

## Entity Configuration

One `IEntityTypeConfiguration<T>` per aggregate, in `Persistence/Configurations/`, named `<Entity>Configuration`.

```csharp
namespace Ordering.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.CustomerId).HasMaxLength(64).IsRequired();
        builder.Property(o => o.Total).HasPrecision(18, 2).IsRequired();
        builder.Property(o => o.PlacedAt).IsRequired();
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(32);

        builder.Property(o => o.RowVersion).IsRowVersion();

        builder.HasMany(o => o.Lines)
               .WithOne()
               .HasForeignKey(l => l.OrderId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(o => new { o.CustomerId, o.PlacedAt });
    }
}
```

- No data-annotation mapping attributes (`[Table]`, `[Column]`, `[MaxLength]`, `[Key]`) on entities. Domain types stay free of persistence concerns.
- Every string property gets an explicit `HasMaxLength`. Every query predicate gets an index.
- Enums persist as strings via `HasConversion<string>()`, so a reordered enum does not silently reinterpret existing rows.

## Money And Time

```csharp
builder.Property(o => o.Total).HasPrecision(18, 2);   // decimal, always
builder.Property(o => o.PlacedAt);                    // DateTimeOffset -> timestamptz
```

- Money is `decimal` with explicit `HasPrecision`. Without it the provider picks a default scale and silently truncates.
- Timestamps are `DateTimeOffset`, produced by an injected `TimeProvider.GetUtcNow()`. Never `DateTime.Now`, never `DateTime.UtcNow`, never a database `now()` default for domain-meaningful times.

## Migrations Are An Explicit, Reviewed Pipeline Step

```bash
# 1. author, locally, against the module project
dotnet ef migrations add AddOrderTotals \
  --project src/Ordering \
  --startup-project src/Api \
  --context OrderingDbContext

# 2. a human reads the generated Up/Down and the snapshot diff

# 3. the deployable artifact is an idempotent SQL script, committed
dotnet ef migrations script --idempotent \
  --project src/Ordering --startup-project src/Api \
  --context OrderingDbContext \
  --output artifacts/migrations/ordering.sql
```

Rules:

- The migration is reviewed like code. An unread migration is an unreviewed schema change.
- Production applies `ordering.sql` as a **deployment step**, before the new app version starts. The app never migrates itself.
- `Database.Migrate()` at startup is forbidden in production: it races across replicas, takes a schema lock under live traffic, and gives the app DDL rights it should not hold.
- `EnsureCreated()` is forbidden in any project that has migrations. It builds a schema from the model directly, writes no history row, and the first real migration then fails or silently diverges. **Mixing `EnsureCreated()` with checked-in migrations is a bug, not a shortcut.**
- Tests build their schema with `MigrateAsync()` against a Testcontainers database — which is also how the migrations themselves get tested.
- Every migration has a reviewed `Down`, or an ADR saying why it is irreversible.

## Reads

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

- `AsNoTracking()` on every read that will not be saved. Tracking costs identity-map bookkeeping for nothing.
- **Project to a DTO** with `Select` / `ProjectToType`. Do not load a whole entity to read two columns — the projection becomes the `SELECT` list.
- `AsSplitQuery()` when a query includes **two or more collection** navigations, otherwise the cartesian product multiplies row counts:

```csharp
await db.Orders
    .AsNoTracking()
    .Include(o => o.Lines)
    .Include(o => o.Adjustments)
    .AsSplitQuery()
    .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
```

Prefer a projection over `Include` entirely; `AsSplitQuery` is for when you genuinely need the graph.

## Pagination Is Mandatory

```csharp
internal sealed record PageRequest(int Page = 0, int Size = 25)
{
    public const int MaxSize = 100;
    public int Take => Math.Clamp(Size, 1, MaxSize);
    public int Skip => Math.Max(Page, 0) * Take;
}
```

```csharp
public async Task<IReadOnlyList<OrderSummary>> HandleAsync(
    PageRequest page, CancellationToken cancellationToken) =>
    await db.Orders
        .AsNoTracking()
        .OrderByDescending(o => o.PlacedAt).ThenBy(o => o.Id)   // stable ordering
        .Skip(page.Skip)
        .Take(page.Take)
        .Select(o => new OrderSummary(o.Id, o.Total, o.PlacedAt))
        .ToListAsync(cancellationToken);
```

- Every list query has `Take` with a **capped** size. There is no "return everything" endpoint.
- `OrderBy` is required and must be deterministic — add a tiebreaker on the key, or pages overlap.
- Prefer keyset pagination (`WHERE (placed_at, id) < (@lastAt, @lastId)`) for deep pages; `Skip` degrades linearly.

## Spotting N+1

The shape: a query returns N rows, then a loop touches a navigation property per row.

```csharp
// N+1: one query for orders, then one per order for lines
var orders = await db.Orders.AsNoTracking().Take(50).ToListAsync(ct);
foreach (var order in orders)
{
    total += order.Lines.Sum(l => l.Total);   // lazy/explicit load per order
}
```

```csharp
// one query
var totals = await db.Orders
    .AsNoTracking()
    .Take(50)
    .Select(o => new { o.Id, LineTotal = o.Lines.Sum(l => l.Total) })
    .ToListAsync(cancellationToken);
```

How to detect it: lazy loading is **not installed**, so an unloaded navigation is empty rather than magically populated — an N+1 usually surfaces as a wrong answer, which is the good outcome. Otherwise enable `LogTo` in Development and look for one statement repeating with different parameters, and assert command counts in an integration test for hot paths.

## Writes

```csharp
public async Task<Result<PlaceOrderResponse>> HandleAsync(
    PlaceOrderRequest request, CancellationToken cancellationToken)
{
    var order = Order.Create(request.CustomerId, pricing.Total(request.Lines), timeProvider.GetUtcNow());

    db.Orders.Add(order);
    await db.SaveChangesAsync(cancellationToken);      // once, at the end

    return Result.Success(new PlaceOrderResponse(order.Id, order.Total));
}
```

- **One `SaveChangesAsync` per unit of work**, at the end of the handler. It is already one transaction; calling it per item turns one round trip into N and abandons atomicity.
- Concurrent updates are guarded by the `RowVersion` token and the conflict surfaces as `DbUpdateConcurrencyException`, which the handler translates into a 409 ProblemDetails — not an unhandled 500.
- Explicit transactions (`BeginTransactionAsync`) only when two `SaveChangesAsync` calls or a raw command must be atomic together, with a comment saying why.
- `CancellationToken` on every async call, always the one threaded from the endpoint.

## Raw SQL

```csharp
var rows = await db.Orders
    .FromSqlInterpolated($"SELECT * FROM ordering.orders WHERE customer_id = {customerId}")
    .AsNoTracking()
    .ToListAsync(cancellationToken);
```

`FromSqlInterpolated` / `FromSql` parameterise the interpolation holes. String concatenation or `string.Format` into `FromSqlRaw` is an injection.

## Seeding

- Seed data for local development and tests lives in the **test/dev** path: a `SeedData` helper called from a fixture or a `Development`-only startup branch.
- Production reference data is a migration or an `INSERT ... ON CONFLICT DO NOTHING` in the SQL script — reviewed, idempotent, versioned.
- Seeding must be Respawn-friendly: because Respawn deletes rows between tests, a fixture that seeds once at container start will lose its data. Seed per test, in `InitializeAsync`, after the reset.
- Never seed from production startup code.

## Forbidden

- `EnsureCreated()` in a project that has migrations.
- `Database.Migrate()` on startup in production.
- Lazy loading — do not install `Microsoft.EntityFrameworkCore.Proxies`, do not use `UseLazyLoadingProxies()`, do not make a navigation `virtual` for it.
- `.ToList()` / `.ToListAsync()` before filtering, ordering, or paging (client-side evaluation of the whole table).
- A list query without `Take`, or with an uncapped caller-supplied page size.
- `Include` chains loading parts of the graph the response never uses.
- Two or more collection `Include`s without `AsSplitQuery()`.
- Raw SQL built by string concatenation or `FromSqlRaw($"... {value} ...")`.
- A `DbContext` captured in a singleton, a static field, or a background service without its own scope.
- `SaveChanges`/`SaveChangesAsync` inside a loop.
- Synchronous `SaveChanges`, `ToList`, `FirstOrDefault` on a request path; any async call missing its `CancellationToken`.
- `double` or `float` for money; `decimal` without `HasPrecision`.
- `DateTime.Now` / `DateTime.UtcNow`; mapping attributes on entities; entities exposed in a response DTO.
