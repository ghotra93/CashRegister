# Typed HTTP Clients And Resilience

## Typed client

One class per external API. `internal sealed`, primary constructor, options-bound base address, never a hand-rolled `HttpClient`.

```csharp
namespace GiftCards.Infrastructure;

internal sealed class GiftCardClient(HttpClient http)
{
    public async Task<GiftCardDto?> FetchAsync(string code, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync($"cards/{Uri.EscapeDataString(code)}", cancellationToken);

        if (response.StatusCode is HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GiftCardDto>(cancellationToken);
    }
}
```

- The client translates transport concerns into domain-shaped returns (`null` for 404). It does not throw `HttpRequestException` past its own boundary for an *expected* outcome.
- `BaseAddress` always ends with `/`; relative paths never start with `/`. Otherwise the path segment is silently dropped.
- Always pass the `CancellationToken`.

## Registration

```csharp
services.AddOptions<GiftCardOptions>()
    .Bind(configuration.GetSection("GiftCards"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

services.AddHttpClient<GiftCardClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<GiftCardOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("ordering/1.0");
})
.AddStandardResilienceHandler();
```

`AddStandardResilienceHandler()` installs, in order: total request timeout → retry → circuit breaker → per-attempt timeout. Do not reimplement any of those layers by hand.

## Tuning the standard handler

```csharp
.AddStandardResilienceHandler(options =>
{
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(4);

    options.Retry.MaxRetryAttempts = 3;
    options.Retry.Delay = TimeSpan.FromMilliseconds(200);
    options.Retry.BackoffType = DelayBackoffType.Exponential;
    options.Retry.UseJitter = true;

    options.CircuitBreaker.FailureRatio = 0.1;
    options.CircuitBreaker.MinimumThroughput = 20;
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
});
```

Invariants the library enforces — get them wrong and startup throws:

- `AttemptTimeout.Timeout` < `TotalRequestTimeout.Timeout`.
- `CircuitBreaker.SamplingDuration` >= 2 × `AttemptTimeout.Timeout`.

| Situation | Setting |
|---|---|
| Idempotent GET against a flaky service | Standard handler, retries on |
| Non-idempotent POST | `AddStandardResilienceHandler()` then `options.Retry.ShouldHandle` narrowed to connect failures only, or disable retry |
| Downstream publishes its own rate limit | Add a rate limiter strategy ahead of retry |
| Long-running export endpoint | Raise `TotalRequestTimeout`, drop retry to 0 |
| Fan-out to many hosts | Use `AddStandardHedgingHandler()` instead |

## Never retry a non-idempotent write blindly

```csharp
.AddStandardResilienceHandler(options =>
{
    options.Retry.ShouldHandle = args => ValueTask.FromResult(
        args.Outcome.Exception is HttpRequestException { HttpRequestError:
            HttpRequestError.ConnectionError or HttpRequestError.NameResolutionError });
});
```

A POST that reached the server and timed out may have succeeded. Either send an idempotency key and retry, or do not retry.

## Custom pipeline when the standard handler does not fit

```csharp
services.AddResiliencePipeline("giftcard-writes", pipeline =>
{
    pipeline
        .AddTimeout(TimeSpan.FromSeconds(5))
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 2,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
        })
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            FailureRatio = 0.2,
            MinimumThroughput = 10,
        });
});
```

```csharp
internal sealed class GiftCardWriter(
    HttpClient http,
    ResiliencePipelineProvider<string> pipelines)
{
    private readonly ResiliencePipeline pipeline = pipelines.GetPipeline("giftcard-writes");

    public Task<HttpResponseMessage> RedeemAsync(RedeemDto dto, CancellationToken cancellationToken) =>
        pipeline.ExecuteAsync(
            async token => await http.PostAsJsonAsync("redemptions", dto, token),
            cancellationToken).AsTask();
}
```

Use a named pipeline only when the per-request strategy differs from the client's default. Prefer one handler per client.

## Observability

- The resilience packages emit metrics under the `Polly` meter (`resilience.polly.strategy.*`) and `IHttpClientFactory` emits `http.client.request.duration`. Wire both into OpenTelemetry; do not add bespoke counters for retry counts.
- Log at the client boundary once, with the downstream name and the outcome. Do not log inside `ShouldHandle`.

## Testing a typed client

Inject a stub `HttpMessageHandler`; never hit the network in a unit test.

```csharp
internal sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });
}

[Fact]
[Trait("AC", "AC-012")]
public async Task FetchAsync_WhenDownstreamReturns404_ReturnsNull()
{
    using var http = new HttpClient(new StubHandler(HttpStatusCode.NotFound, "{}"))
    {
        BaseAddress = new Uri("https://giftcards.test/"),
    };

    var result = await new GiftCardClient(http).FetchAsync("ABC", TestContext.Current.CancellationToken);

    result.ShouldBeNull();
}
```

For a slice test, replace the handler in `WebApplicationFactory` via
`services.AddHttpClient<GiftCardClient>().ConfigurePrimaryHttpMessageHandler(() => new StubHandler(...))`.

## Forbidden here

- `new HttpClient(...)` in production code (a stub handler in a test is the only exception).
- `HttpClient` as a singleton field constructed once by hand — it never picks up DNS changes.
- `IHttpClientFactory.CreateClient("name")` string lookups in a handler; use a typed client.
- Hand-rolled `for` loops with `Task.Delay` as a retry.
- `AddPolicyHandler` / `Microsoft.Extensions.Http.Polly` — superseded by `Microsoft.Extensions.Http.Resilience`.
- Retrying a POST/PATCH without an idempotency key.
