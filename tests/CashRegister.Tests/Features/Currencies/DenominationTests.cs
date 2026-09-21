using CashRegister.Features.Currencies;

namespace CashRegister.Tests.Features.Currencies;

public sealed class DenominationTests
{
    private static readonly Denomination Penny = new("penny", "pennies", 1);

    [Fact]
    [Trait("AC", "AC-009")]
    public void NameFor_CountOfOne_ReturnsSingularName()
    {
        Penny.NameFor(1).ShouldBe("penny");
    }

    [Theory]
    [Trait("AC", "AC-010")]
    [InlineData(2)]
    [InlineData(12)]
    public void NameFor_CountGreaterThanOne_ReturnsPluralName(long count)
    {
        Penny.NameFor(count).ShouldBe("pennies");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Constructor_NonPositiveValue_Throws(long value)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new Denomination("coin", "coins", value));
    }

    [Theory]
    [InlineData("", "coins")]
    [InlineData("coin", " ")]
    public void Constructor_BlankName_Throws(string singular, string plural)
    {
        Should.Throw<ArgumentException>(() => new Denomination(singular, plural, 1));
    }
}
