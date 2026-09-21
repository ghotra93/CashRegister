using CashRegister.Features.Currencies;

namespace CashRegister.Tests.Features.Currencies;

public sealed class CurrencyRegistryTests
{
    private readonly CurrencyRegistry _registry = new([new UsdCurrency()]);

    [Theory]
    [InlineData("USD")]
    [InlineData("usd")]
    public void TryGet_RegisteredCode_IgnoresCaseAndReturnsCurrency(string code)
    {
        _registry.TryGet(code, out var currency).ShouldBeTrue();

        currency.ShouldBeOfType<UsdCurrency>();
    }

    [Fact]
    public void TryGet_UnknownCode_ReturnsFalse()
    {
        _registry.TryGet("EUR", out _).ShouldBeFalse();
    }

    [Fact]
    public void All_ListsEveryRegisteredCurrency()
    {
        _registry.All.Select(c => c.Code).ShouldBe(["USD"]);
    }

    [Fact]
    public void Constructor_DuplicateCodes_Throws()
    {
        Should.Throw<ArgumentException>(() => new CurrencyRegistry([new UsdCurrency(), new UsdCurrency()]));
    }
}
