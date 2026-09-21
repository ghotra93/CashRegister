using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CashRegister.Tests;

/// <summary>
/// The host's startup code (Program.cs) in both environments, run in the unit gate (user decision
/// in T-017, ADR-009): error handling, CORS, OpenAPI exposure and health. The same behaviour is
/// proved end to end by the integration suite; this keeps the composition root inside the unit
/// coverage measurement.
/// </summary>
public sealed class HostCompositionTests
{
    private const string ThrowingPath = "/test-only/throw";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>Adds a route that throws, behind the host's exception handler.</summary>
    private sealed class ThrowingEndpointFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.Map(ThrowingPath, branch => branch.Run(_ => throw new InvalidOperationException("boom")));
        };
    }

    private static WebApplicationFactory<Program> HostIn(string environment) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, ThrowingEndpointFilter>());
        });

    private static async Task<bool> PreflightAllowed(HttpClient client)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, new Uri("/api/files", UriKind.Relative));
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        var response = await client.SendAsync(request, Cancellation);
        return response.Headers.Contains("Access-Control-Allow-Origin");
    }

    [Theory]
    [Trait("AC", "AC-015")]
    [InlineData("Development", true, HttpStatusCode.OK)]
    [InlineData("Production", false, HttpStatusCode.NotFound)]
    public async Task Host_ComposesTheSamePipelineInEachEnvironment(
        string environment, bool spaOriginAllowed, HttpStatusCode openApiStatus)
    {
        await using var factory = HostIn(environment);
        var client = factory.CreateClient();

        (await client.GetStringAsync(new Uri("/health", UriKind.Relative), Cancellation)).ShouldBe("Healthy");

        var badBody = await client.PutAsync(
            new Uri("/api/settings/divisor", UriKind.Relative),
            new StringContent("not json", Encoding.UTF8, "application/json"),
            Cancellation);
        badBody.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        badBody.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var crash = await client.GetAsync(new Uri(ThrowingPath, UriKind.Relative), Cancellation);
        crash.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        (await crash.Content.ReadAsStringAsync(Cancellation)).ShouldNotContain("boom");

        (await PreflightAllowed(client)).ShouldBe(spaOriginAllowed);

        (await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), Cancellation)).StatusCode
            .ShouldBe(openApiStatus);
    }
}
