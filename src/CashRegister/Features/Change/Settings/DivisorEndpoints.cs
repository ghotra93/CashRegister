using CashRegister.Features.Change.Processing;
using CashRegister.Features.Change.Rules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace CashRegister.Features.Change.Settings;

/// <summary><c>/api/settings/divisor</c>: read or change the special-case divisor from the UI (AC-025).</summary>
internal static class DivisorEndpoints
{
    private const string LoggerCategory = "CashRegister.Features.Change.Settings.DivisorEndpoints";

    public static IEndpointRouteBuilder MapDivisorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var divisor = endpoints.MapGroup("/api/settings/divisor").WithTags("Settings");

        divisor.MapGet("/", Get);
        divisor.MapPut("/", Change);

        return endpoints;
    }

    private static Ok<DivisorResponse> Get(IDivisorSettings settings) =>
        TypedResults.Ok(new DivisorResponse(settings.Current));

    private static Results<Ok<DivisorResponse>, ProblemHttpResult> Change(
        ChangeDivisorRequest request,
        IDivisorSettings settings,
        ILoggerFactory loggerFactory)
    {
        if (request.Divisor is not { } newDivisor || newDivisor < InMemoryDivisorSettings.MinimumDivisor)
        {
            return TypedResults.Problem(
                title: "Invalid divisor",
                detail: $"The divisor must be a whole number of at least {InMemoryDivisorSettings.MinimumDivisor}.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var oldDivisor = settings.Current;
        settings.Change(newDivisor);
        loggerFactory.CreateLogger(LoggerCategory).DivisorChanged(oldDivisor, newDivisor);

        return TypedResults.Ok(new DivisorResponse(newDivisor));
    }
}
