using CashRegister.Features.Change.Money;
using CashRegister.Features.Change.Strategies;
using CashRegister.Features.Currencies;

namespace CashRegister.Tests.Features.Change.Strategies;

public sealed class MinimalChangeStrategyTests
{
    private static readonly UsdCurrency Usd = new();
    private readonly MinimalChangeStrategy _strategy = new();

    private static IEnumerable<(string Name, long Count)> Describe(IReadOnlyList<ChangeLine> lines) =>
        lines.Select(l => (l.Denomination.SingularName, l.Count));

    [Fact]
    [Trait("AC", "AC-003")]
    public void MakeChange_88Cents_Is3Quarters1Dime3Pennies()
    {
        var lines = _strategy.MakeChange(88, Usd);

        Describe(lines).ShouldBe([("quarter", 3L), ("dime", 1L), ("penny", 3L)]);
    }

    [Fact]
    [Trait("AC", "AC-003")]
    public void MakeChange_3Cents_Is3Pennies()
    {
        Describe(_strategy.MakeChange(3, Usd)).ShouldBe([("penny", 3L)]);
    }

    [Fact]
    [Trait("AC", "AC-003")]
    public void MakeChange_167Cents_Is1Dollar2Quarters1Dime1Nickel2Pennies()
    {
        Describe(_strategy.MakeChange(167, Usd))
            .ShouldBe([("dollar", 1L), ("quarter", 2L), ("dime", 1L), ("nickel", 1L), ("penny", 2L)]);
    }

    [Fact]
    public void MakeChange_Zero_IsEmpty()
    {
        _strategy.MakeChange(0, Usd).ShouldBeEmpty();
    }

    [Fact]
    [Trait("AC", "AC-005")]
    public void MakeChange_AnyAmountUpTo1000Cents_SumsExactlyToAmount()
    {
        for (long amount = 0; amount <= 1000; amount++)
        {
            _strategy.MakeChange(amount, Usd).Sum(l => l.Total).ShouldBe(amount, $"amount {amount}");
        }
    }

    [Fact]
    public void MakeChange_NegativeAmount_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => _strategy.MakeChange(-1, Usd));
    }
}
