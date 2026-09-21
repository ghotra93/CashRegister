using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Testing;

namespace CashRegister.IntegrationTests.Features.Change.Settings;

/// <summary>
/// Each test starts its own host: the divisor is process-wide state, so a shared
/// fixture would let one test's change leak into another.
/// </summary>
public sealed class DivisorEndpointsTests
{
    private static readonly Uri DivisorUri = new("/api/settings/divisor", UriKind.Relative);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private sealed record DivisorBody(int Divisor);

    private static Task<HttpResponseMessage> PutRaw(HttpClient client, string json) =>
        client.PutAsync(DivisorUri, new StringContent(json, Encoding.UTF8, "application/json"), Cancellation);

    private static async Task<string> UploadAndDownload(HttpClient client, string line)
    {
        using var form = new MultipartFormDataContent { { new ByteArrayContent(Encoding.UTF8.GetBytes(line)), "file", "one.txt" } };
        var created = await client.PostAsync(new Uri("/api/files", UriKind.Relative), form, Cancellation);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);

        return await client.GetStringAsync(new Uri(created.Headers.Location + "/output", UriKind.Relative), Cancellation);
    }

    private static async Task<ProblemDetails> ShouldBeBadRequestProblem(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation);
        problem.ShouldNotBeNull();
        return problem;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Get_Initially_ReturnsThree()
    {
        await using var factory = new CashRegisterApiFactory();
        var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<DivisorBody>(DivisorUri, Cancellation);

        body.ShouldBe(new DivisorBody(3));
    }

    [Fact]
    [Trait("AC", "AC-025")]
    [Trait("Category", "Integration")]
    public async Task Put_ValidDivisor_ReturnsItAndGetReflectsIt()
    {
        await using var factory = new CashRegisterApiFactory();
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(DivisorUri, new DivisorBody(5), Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<DivisorBody>(Cancellation)).ShouldBe(new DivisorBody(5));
        (await client.GetFromJsonAsync<DivisorBody>(DivisorUri, Cancellation)).ShouldBe(new DivisorBody(5));
    }

    [Fact]
    [Trait("AC", "AC-025")]
    [Trait("Category", "Integration")]
    public async Task Put_NewDivisor_AppliesToFilesProcessedAfterwards()
    {
        await using var factory = new CashRegisterApiFactory();
        var client = factory.CreateClient();

        // 333 is not divisible by 5, so after the change 3.33,5.00 must get exact minimal change.
        (await client.PutAsJsonAsync(DivisorUri, new DivisorBody(5), Cancellation)).EnsureSuccessStatusCode();

        (await UploadAndDownload(client, "3.33,5.00\n"))
            .ShouldBe("1 dollar,2 quarters,1 dime,1 nickel,2 pennies");
    }

    [Theory]
    [Trait("AC", "AC-026")]
    [Trait("Category", "Integration")]
    [InlineData("{\"divisor\":0}")]
    [InlineData("{\"divisor\":-2}")]
    [InlineData("{}")]
    public async Task Put_DivisorBelowOneOrMissing_Returns400ProblemDetailsAndKeepsTheOldValue(string json)
    {
        await using var factory = new CashRegisterApiFactory();
        var client = factory.CreateClient();

        var response = await PutRaw(client, json);

        (await ShouldBeBadRequestProblem(response)).Title.ShouldBe("Invalid divisor");
        (await client.GetFromJsonAsync<DivisorBody>(DivisorUri, Cancellation)).ShouldBe(new DivisorBody(3));
    }

    [Theory]
    [Trait("AC", "AC-015")]
    [Trait("Category", "Integration")]
    [InlineData("{\"divisor\":\"abc\"}")]
    [InlineData("{\"divisor\":2.5}")]
    [InlineData("not json")]
    public async Task Put_NonIntegerDivisor_Returns400ProblemDetails(string json)
    {
        await using var factory = new CashRegisterApiFactory();

        await ShouldBeBadRequestProblem(await PutRaw(factory.CreateClient(), json));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Put_ValidDivisor_LogsDivisorChanged()
    {
        await using var factory = new CashRegisterApiFactory();
        await using var logged = factory.WithWebHostBuilder(b => b.ConfigureServices(s => s.AddFakeLogging()));

        (await logged.CreateClient().PutAsJsonAsync(DivisorUri, new DivisorBody(7), Cancellation)).EnsureSuccessStatusCode();

        var record = logged.Services.GetRequiredService<FakeLogCollector>()
            .GetSnapshot()
            .Single(r => r.Id.Name == "DivisorChanged");
        record.StructuredState.ShouldNotBeNull();
        record.StructuredState.ShouldContain(new KeyValuePair<string, string?>("OldDivisor", "3"));
        record.StructuredState.ShouldContain(new KeyValuePair<string, string?>("NewDivisor", "7"));
    }
}
