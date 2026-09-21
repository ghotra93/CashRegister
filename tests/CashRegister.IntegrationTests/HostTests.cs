using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace CashRegister.IntegrationTests;

public sealed class HostTests(CashRegisterApiFactory factory) : IClassFixture<CashRegisterApiFactory>
{
    private const string SpaOrigin = "http://localhost:5173";
    private readonly HttpClient _client = factory.CreateClient();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Health_ReturnsHealthy()
    {
        var response = await _client.GetAsync(new Uri("/health", UriKind.Relative), Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(Cancellation)).ShouldBe("Healthy");
    }

    [Fact]
    [Trait("AC", "AC-015")]
    [Trait("Category", "Integration")]
    public async Task UnknownApiRoute_ReturnsNotFoundProblemDetails()
    {
        var response = await _client.GetAsync(new Uri("/api/does-not-exist", UriKind.Relative), Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation);
        problem.ShouldNotBeNull();
        problem.Status.ShouldBe(404);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CorsPreflight_FromTheSpaOrigin_IsAllowed()
    {
        var response = await SendPreflight(SpaOrigin);

        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins).ShouldBeTrue();
        origins.ShouldBe([SpaOrigin]);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CorsPreflight_FromAnotherOrigin_IsNotAllowed()
    {
        var response = await SendPreflight("https://evil.example");

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    private async Task<HttpResponseMessage> SendPreflight(string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, new Uri("/api/files", UriKind.Relative));
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");

        return await _client.SendAsync(request, Cancellation);
    }
}
