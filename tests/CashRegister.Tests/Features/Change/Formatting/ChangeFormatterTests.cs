using CashRegister.Features.Change.Formatting;
using CashRegister.Features.Change.Money;
using CashRegister.Features.Change.Strategies;
using CashRegister.Features.Currencies;

namespace CashRegister.Tests.Features.Change.Formatting;

public sealed class ChangeFormatterTests
{
    private static readonly UsdCurrency Usd = new();
    private static readonly Denomination Dollar = Usd.Denominations[0];
    private static readonly Denomination Quarter = Usd.Denominations[1];
    private static readonly Denomination Dime = Usd.Denominations[2];
    private static readonly Denomination Nickel = Usd.Denominations[3];
    private static readonly Denomination Penny = Usd.Denominations[4];

    [Fact]
    [Trait("AC", "AC-006")]
    public void Format_SeveralDenominations_IsCommaSeparatedCountAndName()
    {
        ChangeFormatter.Format([new(Dollar, 1), new(Quarter, 2), new(Nickel, 1)])
            .ShouldBe("1 dollar,2 quarters,1 nickel");
    }

    [Fact]
    [Trait("AC", "AC-007")]
    public void Format_UnorderedLines_ListsLargestDenominationFirst()
    {
        ChangeFormatter.Format([new(Penny, 12), new(Dollar, 1), new(Nickel, 6), new(Quarter, 1)])
            .ShouldBe("1 dollar,1 quarter,6 nickels,12 pennies");
    }

    [Fact]
    [Trait("AC", "AC-008")]
    public void Format_ZeroCountLine_IsOmitted()
    {
        ChangeFormatter.Format([new(Quarter, 3), new(Dime, 0), new(Penny, 3)])
            .ShouldBe("3 quarters,3 pennies");
    }

    [Fact]
    [Trait("AC", "AC-009")]
    public void Format_CountOfOne_UsesSingularName()
    {
        ChangeFormatter.Format([new(Penny, 1)]).ShouldBe("1 penny");
    }

    [Fact]
    [Trait("AC", "AC-010")]
    public void Format_CountAboveOne_UsesPluralName()
    {
        ChangeFormatter.Format([new(Penny, 3)]).ShouldBe("3 pennies");
    }

    [Fact]
    [Trait("AC", "AC-011")]
    public void Format_MinimalChangeFor2_12Paid3_00_MatchesReadmeSample()
    {
        var lines = new MinimalChangeStrategy().MakeChange(300 - 212, Usd);

        ChangeFormatter.Format(lines).ShouldBe("3 quarters,1 dime,3 pennies");
    }

    [Fact]
    [Trait("AC", "AC-012")]
    public void Format_MinimalChangeFor1_97Paid2_00_MatchesReadmeSample()
    {
        var lines = new MinimalChangeStrategy().MakeChange(200 - 197, Usd);

        ChangeFormatter.Format(lines).ShouldBe("3 pennies");
    }

    [Fact]
    [Trait("AC", "AC-016")]
    public void Format_NoLines_IsNoChange()
    {
        ChangeFormatter.Format([]).ShouldBe("No change");
    }

    [Fact]
    [Trait("AC", "AC-016")]
    public void Format_OnlyZeroCountLines_IsNoChange()
    {
        ChangeFormatter.Format([new(Penny, 0)]).ShouldBe(ChangeFormatter.NoChange);
    }
}
