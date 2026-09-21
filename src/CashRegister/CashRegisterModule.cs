using CashRegister.Features.Change;
using CashRegister.Features.Change.Processing;
using CashRegister.Features.Change.Rules;
using CashRegister.Features.Change.Strategies;
using CashRegister.Features.Currencies;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CashRegister;

/// <summary>The module's public surface: register its services and map its endpoints (ADR-001).</summary>
public static class CashRegisterModule
{
    public static IServiceCollection AddCashRegister(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Currencies (DC-002: a new currency is one more registration).
        services.AddSingleton<Currency, UsdCurrency>();
        services.AddSingleton<ICurrencyRegistry>(sp => new CurrencyRegistry(sp.GetServices<Currency>()));

        services.AddOptions<CashRegisterOptions>()
            .Bind(configuration.GetSection(CashRegisterOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<CashRegisterOptions>, CashRegisterOptionsValidator>();

        services.AddSingleton(sp =>
        {
            // Reading .Value runs CashRegisterOptionsValidator, so the code is known to be registered.
            var code = sp.GetRequiredService<IOptions<CashRegisterOptions>>().Value.Currency;
            sp.GetRequiredService<ICurrencyRegistry>().TryGet(code, out var currency);
            return new ActiveCurrency(currency!);
        });

        // Change strategies and special-case rules (DC-001: a new rule is one more registration).
        services.AddSingleton<IChangeStrategy, MinimalChangeStrategy>();
        services.AddSingleton<IChangeStrategy>(new RandomChangeStrategy(Random.Shared));
        services.AddSingleton<IDivisorSettings, InMemoryDivisorSettings>();
        services.AddSingleton<IChangeRule, OwedDivisibleByRule>();

        services.AddSingleton<ChangeCalculator>();
        services.AddSingleton<ChangeFileProcessor>();

        return services;
    }

    public static IEndpointRouteBuilder MapCashRegister(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        return endpoints;
    }
}
