using CashRegister.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace CashRegister.Tests;

/// <summary>Host composition as plain, testable functions (review finding F-001, T-018).</summary>
public sealed class HostSetupTests
{
    private static WebApplication BuildApp(string environment)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.AddCashRegisterHost();
        var app = builder.Build();
        app.UseCashRegisterHost();
        return app;
    }

    private static List<string> RoutePatterns(WebApplication app) =>
    [
        .. ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty),
    ];

    [Theory]
    [Trait("AC", "AC-015")]
    [InlineData(StatusCodes.Status400BadRequest)]
    [InlineData(StatusCodes.Status413PayloadTooLarge)]
    public void StatusCodeFor_BadRequestException_KeepsItsOwnStatus(int status)
    {
        HostSetup.StatusCodeFor(new BadHttpRequestException("bad request", status)).ShouldBe(status);
    }

    [Fact]
    [Trait("AC", "AC-015")]
    public void StatusCodeFor_AnyOtherException_Is500()
    {
        HostSetup.StatusCodeFor(new InvalidOperationException("boom")).ShouldBe(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    [Trait("AC", "AC-015")]
    public void AllowedOrigins_ConfiguredList_IsReturned()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Cors:AllowedOrigins:0"] = "http://localhost:5173" })
            .Build();

        HostSetup.AllowedOrigins(configuration).ShouldBe(["http://localhost:5173"]);
    }

    [Fact]
    [Trait("AC", "AC-015")]
    public void AllowedOrigins_MissingSection_IsEmpty()
    {
        HostSetup.AllowedOrigins(new ConfigurationBuilder().Build()).ShouldBeEmpty();
    }

    [Theory]
    [Trait("AC", "AC-015")]
    [InlineData("Production", ConsoleFormatterNames.Json)]
    [InlineData("Development", ConsoleFormatterNames.Simple)]
    public async Task AddCashRegisterHost_UsesJsonConsoleLogsOutsideDevelopmentOnly(string environment, string formatter)
    {
        await using var app = BuildApp(environment);

        var options = app.Services.GetRequiredService<IOptions<ConsoleLoggerOptions>>().Value;

        (options.FormatterName ?? ConsoleFormatterNames.Simple).ShouldBe(formatter);
    }

    [Theory]
    [Trait("AC", "AC-015")]
    [InlineData("Development", true)]
    [InlineData("Production", false)]
    public async Task UseCashRegisterHost_MapsOpenApiInDevelopmentOnly(string environment, bool openApiMapped)
    {
        await using var app = BuildApp(environment);

        RoutePatterns(app).Any(pattern => pattern.Contains("openapi", StringComparison.Ordinal)).ShouldBe(openApiMapped);
    }

    [Fact]
    [Trait("AC", "AC-015")]
    public async Task UseCashRegisterHost_MapsHealthAndTheModuleEndpoints()
    {
        await using var app = BuildApp("Production");

        var routes = RoutePatterns(app);

        routes.ShouldContain("/health");
        routes.ShouldContain("/api/files/");
        routes.ShouldContain("/api/settings/divisor/");
    }
}
