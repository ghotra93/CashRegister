using CashRegister.Features.Change;
using CashRegister.Features.Change.Processing;
using CashRegister.Features.Change.Rules;
using CashRegister.Features.Change.Strategies;
using CashRegister.Features.Currencies;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CashRegister.Tests;

public sealed class CashRegisterModuleTests
{
    private static ServiceProvider BuildProvider(string? currencyCode = null)
    {
        var settings = new Dictionary<string, string?>();
        if (currencyCode is not null)
        {
            settings["CashRegister:Currency"] = currencyCode;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCashRegister(configuration);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    [Fact]
    public void AddCashRegister_ResolvesTheFileProcessor()
    {
        using var provider = BuildProvider();

        provider.GetService<ChangeFileProcessor>().ShouldNotBeNull();
    }

    [Fact]
    public void AddCashRegister_RegistersBothChangeStrategies()
    {
        using var provider = BuildProvider();

        provider.GetServices<IChangeStrategy>().Select(s => s.GetType())
            .ShouldBe([typeof(MinimalChangeStrategy), typeof(RandomChangeStrategy)], ignoreOrder: true);
    }

    [Fact]
    public void AddCashRegister_RegistersTheDivisibleByRuleAndASingleDivisorSetting()
    {
        using var provider = BuildProvider();

        provider.GetServices<IChangeRule>().Single().ShouldBeOfType<OwedDivisibleByRule>();
        provider.GetRequiredService<IDivisorSettings>()
            .ShouldBeSameAs(provider.GetRequiredService<IDivisorSettings>());
        provider.GetService<ChangeCalculator>().ShouldNotBeNull();
    }

    [Fact]
    public void ActiveCurrency_DefaultsToUsd()
    {
        using var provider = BuildProvider();

        provider.GetRequiredService<ActiveCurrency>().Currency.ShouldBeOfType<UsdCurrency>();
    }

    [Fact]
    public void ActiveCurrency_ConfiguredCodeIsCaseInsensitive()
    {
        using var provider = BuildProvider("usd");

        provider.GetRequiredService<ActiveCurrency>().Currency.Code.ShouldBe("USD");
    }

    [Fact]
    public void Options_UnknownCurrency_FailValidation()
    {
        using var provider = BuildProvider("XYZ");

        var exception = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<CashRegisterOptions>>().Value);

        exception.Message.ShouldContain("XYZ");
    }
}
