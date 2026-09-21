using System.Net;
using System.Text.Json;

namespace CashRegister.IntegrationTests;

/// <summary>
/// Contract smoke test: the generated OpenAPI document (Development only) describes every
/// public endpoint. A full snapshot comparison needs the Verify package (see 06-test-plan.md, Gap-004).
/// </summary>
public sealed class OpenApiDocumentTests(CashRegisterApiFactory factory) : IClassFixture<CashRegisterApiFactory>
{
    [Fact]
    [Trait("AC", "AC-027")]
    [Trait("Category", "Integration")]
    public async Task OpenApiDocument_DescribesEveryPublicEndpoint()
    {
        var response = await factory.CreateClient()
            .GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var paths = document.RootElement.GetProperty("paths");

        OperationsOf(paths, "/api/files").ShouldBe(["get", "post"], ignoreOrder: true);
        OperationsOf(paths, "/api/files/{id}/output").ShouldBe(["get"]);
        OperationsOf(paths, "/api/settings/divisor").ShouldBe(["get", "put"], ignoreOrder: true);
    }

    private static List<string> OperationsOf(JsonElement paths, string path)
    {
        paths.TryGetProperty(path, out var item).ShouldBeTrue($"OpenAPI document is missing {path}");
        return [.. item.EnumerateObject().Select(p => p.Name)];
    }
}
