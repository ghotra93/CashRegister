using CashRegister.Features.Currencies;

namespace CashRegister.Tests.Features.Currencies;

public sealed class UsdCurrencyTests
{
    private readonly UsdCurrency _usd = new();

    [Fact]
    public void Code_IsUsd()
    {
        _usd.Code.ShouldBe("USD");
    }

    [Fact]
    [Trait("AC", "AC-007")]
    public void Denominations_AreDollarQuarterDimeNickelPenny_LargestFirst()
    {
        _usd.Denominations.Select(d => (d.SingularName, d.ValueInMinorUnits)).ShouldBe(
        [
            ("dollar", 100L),
            ("quarter", 25L),
            ("dime", 10L),
            ("nickel", 5L),
            ("penny", 1L),
        ]);
    }

    [Fact]
    public void Denominations_HaveEnglishPluralNames()
    {
        _usd.Denominations.Select(d => d.PluralName)
            .ShouldBe(["dollars", "quarters", "dimes", "nickels", "pennies"]);
    }

    [Fact]
    [Trait("AC", "AC-029")]
    public void DecimalSeparator_IsDot()
    {
        _usd.DecimalSeparator.ShouldBe('.');
    }

    [Fact]
    [Trait("AC", "AC-029")]
    public void MinorUnitDigits_IsTwo()
    {
        _usd.MinorUnitDigits.ShouldBe(2);
    }

    private sealed class TestCurrency(IEnumerable<Denomination> denominations)
        : Currency("TST", '.', 2, denominations);

    [Fact]
    public void Constructor_WithoutOneMinorUnitDenomination_Throws()
    {
        Should.Throw<ArgumentException>(() => new TestCurrency([new("five", "fives", 5)]));
    }

    [Fact]
    public void Constructor_WithDuplicateValues_Throws()
    {
        Should.Throw<ArgumentException>(() => new TestCurrency(
        [
            new("unit", "units", 1),
            new("other unit", "other units", 1),
        ]));
    }

    [Fact]
    public void Constructor_WithNoDenominations_Throws()
    {
        Should.Throw<ArgumentException>(() => new TestCurrency([]));
    }

    [Fact]
    [Trait("AC", "AC-007")]
    public void Constructor_UnorderedDenominations_StoresThemLargestFirst()
    {
        var currency = new TestCurrency(
        [
            new("unit", "units", 1),
            new("ten", "tens", 10),
            new("five", "fives", 5),
        ]);

        currency.Denominations.Select(d => d.ValueInMinorUnits).ShouldBe([10L, 5L, 1L]);
    }
}
