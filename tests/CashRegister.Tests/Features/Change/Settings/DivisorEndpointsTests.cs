using CashRegister.Features.Change.Rules;
using CashRegister.Features.Change.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace CashRegister.Tests.Features.Change.Settings;

/// <summary>Unit tests for the /api/settings/divisor handlers, called directly without a host.</summary>
public sealed class DivisorEndpointsTests : IDisposable
{
    private readonly InMemoryDivisorSettings _settings = new();
    private readonly FakeLoggerProvider _logs = new();
    private readonly LoggerFactory _loggerFactory;

    public DivisorEndpointsTests()
    {
        _loggerFactory = new LoggerFactory([_logs]);
    }

    public void Dispose()
    {
        _loggerFactory.Dispose();
        _logs.Dispose();
    }

    [Fact]
    [Trait("AC", "AC-025")]
    public void Get_ReturnsTheCurrentDivisor()
    {
        _settings.Change(7);

        DivisorEndpoints.Get(_settings).Value.ShouldBe(new DivisorResponse(7));
    }

    [Fact]
    [Trait("AC", "AC-025")]
    public void Change_ValidDivisor_UpdatesTheSettingAndReturnsIt()
    {
        var ok = DivisorEndpoints.Change(new ChangeDivisorRequest(5), _settings, _loggerFactory)
            .Result.ShouldBeOfType<Ok<DivisorResponse>>();

        ok.Value.ShouldBe(new DivisorResponse(5));
        _settings.Current.ShouldBe(5);
    }

    [Fact]
    [Trait("AC", "AC-025")]
    public void Change_ValidDivisor_LogsTheOldAndNewValues()
    {
        DivisorEndpoints.Change(new ChangeDivisorRequest(5), _settings, _loggerFactory);

        var record = _logs.Collector.GetSnapshot().Single(r => r.Id.Name == "DivisorChanged");
        record.StructuredState.ShouldNotBeNull();
        record.StructuredState.ShouldContain(new KeyValuePair<string, string?>("OldDivisor", "3"));
        record.StructuredState.ShouldContain(new KeyValuePair<string, string?>("NewDivisor", "5"));
    }

    [Fact]
    [Trait("AC", "AC-025")]
    public async Task MapCashRegister_RegistersTheDivisorRoutes()
    {
        await using var app = Files.FileEndpointsTests.RouteTable.BuildApp();

        string[] expected = ["GET /api/settings/divisor/", "PUT /api/settings/divisor/"];

        expected.ShouldBeSubsetOf(Files.FileEndpointsTests.RouteTable.Routes(app));
    }

    [Theory]
    [Trait("AC", "AC-026")]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-2)]
    public void Change_MissingOrBelowOne_ReturnsInvalidDivisorAndKeepsTheValue(int? divisor)
    {
        var problem = DivisorEndpoints.Change(new ChangeDivisorRequest(divisor), _settings, _loggerFactory)
            .Result.ShouldBeOfType<ProblemHttpResult>();

        problem.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        problem.ProblemDetails.Title.ShouldBe("Invalid divisor");
        _settings.Current.ShouldBe(InMemoryDivisorSettings.DefaultDivisor);
        _logs.Collector.Count.ShouldBe(0);
    }
}
