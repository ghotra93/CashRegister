# Integration Testing — WebApplicationFactory, Testcontainers, Respawn

## xUnit v3 on Microsoft.Testing.Platform

Every test project is a real executable. There is no `vstest`, no `xunit.runner.visualstudio`.

```xml
<!-- tests/Ordering.IntegrationTests/Ordering.IntegrationTests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
    <UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>
    <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
    <PackageReference Include="Testcontainers.PostgreSql" />
    <PackageReference Include="Respawn" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../../src/Api/Api.csproj" />
  </ItemGroup>

</Project>
```

`tests/Directory.Build.props` already supplies `xunit.v3`, the coverage extension, NSubstitute, Shouldly, Verify and `FakeTimeProvider`, plus the MTP properties above. No `Version=` attributes anywhere — central package management owns every version (see the `dotnet-build-harness` skill).

`WebApplicationFactory<Program>` needs `Program` to be reachable. Top-level statements make it `internal`, so the host project declares:

```xml
<ItemGroup>
  <InternalsVisibleTo Include="Ordering.Tests" />
  <InternalsVisibleTo Include="Ordering.IntegrationTests" />
</ItemGroup>
```

`InternalsVisibleTo` is allowed **only** for test assemblies. Using it to let one module see another module's internals is an architecture violation.

## Parallelism

Container-backed classes must not race each other for the same database. Put this in `tests/Ordering.IntegrationTests/AssemblyInfo.cs`:

```csharp
[assembly: CollectionBehavior(DisableTestParallelization = true)]
```

Serialising the integration suite is cheaper than debugging cross-talk.

## Testcontainers fixture

```csharp
namespace Ordering.IntegrationTests.Support;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")          // pinned, never :latest
        .WithDatabase("ordering")
        .WithCleanUp(true)
        .Build();

    public string ConnectionString => container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync();
        await ApplyMigrationsAsync();
    }

    public async ValueTask DisposeAsync() => await container.DisposeAsync();

    private async Task ApplyMigrationsAsync()
    {
        var options = new DbContextOptionsBuilder<OrderingDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        await using var db = new OrderingDbContext(options);
        await db.Database.MigrateAsync();          // migrations, never EnsureCreated
    }
}
```

- xUnit v3 `IAsyncLifetime` returns `ValueTask`, not `Task`.
- The schema is created by **running the checked-in migrations**. `EnsureCreated()` would build a schema the migrations never produced and hide migration bugs.

## Sharing one container across classes

One container per class is correct but slow. Share it with a collection fixture:

```csharp
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
```

```csharp
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class OrderRepositoryTests(PostgresFixture postgres)
{
    [Fact]
    [Trait("AC", "AC-014")]
    public async Task SaveChangesAsync_WithNewOrder_PersistsTotalWithScale2()
    {
        await using var db = postgres.NewContext();

        db.Orders.Add(Orders.Placed(total: 12.34m));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var reloaded = await db.Orders.AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken);
        reloaded.Total.ShouldBe(12.34m);
    }
}
```

| Need | Fixture |
|---|---|
| Container used by one test class | `IClassFixture<PostgresFixture>` |
| Container shared across several classes | `ICollectionFixture<PostgresFixture>` + `[Collection]` |
| Per-test clean database | Shared container + Respawn reset in the constructor |

## Respawn — resetting state between tests

Dropping and recreating the schema per test costs seconds. Respawn deletes rows and leaves the schema alone.

```csharp
public sealed class PostgresFixture : IAsyncLifetime
{
    private Respawner respawner = null!;

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync();
        await ApplyMigrationsAsync();

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = [new Respawn.Graph.Table("__EFMigrationsHistory")],
        });
    }

    public async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await respawner.ResetAsync(connection);
    }
}
```

Call `ResetAsync()` from the test class's `InitializeAsync`, never from `DisposeAsync` — a crashed test then still leaves a clean slate for the next one.

`TablesToIgnore` must include `__EFMigrationsHistory`, or the second test in the class runs against a schema EF believes is unmigrated.

## WebApplicationFactory

```csharp
public sealed class OrderingAppFactory(PostgresFixture postgres)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Ordering"] = postgres.ConnectionString,
                ["GiftCards:BaseUrl"] = "https://giftcards.test/",
            }));

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(
                new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));

            services.AddHttpClient<GiftCardClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => new StubGiftCardHandler());
        });
    }
}
```

- Use `ConfigureTestServices`, not `ConfigureServices` — it runs **after** the app's own registrations, so replacements win.
- Replace with `RemoveAll<T>()` then `Add...`. A bare `AddSingleton` only appends; the original registration may still be resolved.
- Override configuration through `AddInMemoryCollection`, not by shipping an `appsettings.Testing.json` full of test hostnames.
- Every outbound `HttpClient` is stubbed. An integration test may talk to its own container; it never talks to the internet.

One-off variation without a new factory:

```csharp
using var client = factory.WithWebHostBuilder(builder =>
    builder.ConfigureTestServices(services =>
    {
        services.RemoveAll<IOrderPricing>();
        services.AddSingleton<IOrderPricing>(new ThrowingPricing());
    })).CreateClient();
```

`WithWebHostBuilder` builds a **new** host — call it once per test and dispose the client.

## A full end-to-end test

```csharp
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class PlaceOrderEndpointTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly OrderingAppFactory factory = new(postgres);

    public async ValueTask InitializeAsync() => await postgres.ResetAsync();

    public async ValueTask DisposeAsync() => await factory.DisposeAsync();

    [Fact]
    [Trait("AC", "AC-002")]
    public async Task Post_WithValidOrder_Returns201AndPersistsOrder()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/orders", Requests.Valid(), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();

        await using var db = postgres.NewContext();
        (await db.Orders.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
    }
}
```

## Checklist before committing an integration test

- `[Trait("Category", "Integration")]` on the class.
- `[Trait("AC", "AC-NNN")]` on every method.
- Image tag pinned.
- Schema from migrations, not `EnsureCreated()`.
- State reset in `InitializeAsync` via Respawn.
- No outbound HTTP left unstubbed.
- `TestContext.Current.CancellationToken` passed to every async call.
- Passes when run alone, and passes twice in a row.
