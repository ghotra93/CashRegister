using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CashRegister.IntegrationTests;

/// <summary>
/// The host as configured outside Development: JSON console logging, no CORS origins,
/// and 500 ProblemDetails that do not leak exception details (03-design.md, ADR-005).
/// </summary>
public sealed class ProductionHostTests : IAsyncLifetime
{
    private const string ThrowingPath = "/test-only/throw";
    private const string SecretMessage = "secret internal detail";

    private readonly WebApplicationFactory<Program> _factory = new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, ThrowingEndpointFilter>());
        });

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>Adds a route that throws, after the host's exception handler, so its 500 path can be observed.</summary>
    private sealed class ThrowingEndpointFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.Map(ThrowingPath, branch => branch.Run(_ => throw new InvalidOperationException(SecretMessage)));
        };
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    [Trait("AC", "AC-015")]
    [Trait("Category", "Integration")]
    public async Task UnhandledException_Returns500ProblemDetailsWithoutInternalDetails()
    {
        var response = await _factory.CreateClient().GetAsync(new Uri(ThrowingPath, UriKind.Relative), Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var body = await response.Content.ReadAsStringAsync(Cancellation);
        body.ShouldNotContain(SecretMessage);
        body.ShouldNotContain(nameof(InvalidOperationException));
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Status.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    [Trait("AC", "AC-015")]
    [Trait("Category", "Integration")]
    public async Task Production_AllowsNoCrossOriginRequestsByDefault()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, new Uri("/api/files", UriKind.Relative));
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await _factory.CreateClient().SendAsync(request, Cancellation);

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    [Fact]
    [Trait("AC", "AC-015")]
    [Trait("Category", "Integration")]
    public async Task Production_DoesNotExposeTheOpenApiDocument()
    {
        var response = await _factory.CreateClient().GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
