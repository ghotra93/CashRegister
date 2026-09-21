using CashRegister.Features.Change.Money;
using CashRegister.Features.Change.Rules;
using CashRegister.Features.Change.Strategies;
using CashRegister.Features.Currencies;

namespace CashRegister.Tests.Features.Change.Rules;

public sealed class OwedDivisibleByRuleTests
{
    private static readonly UsdCurrency Usd = new();
    private readonly InMemoryDivisorSettings _settings = new();

    private bool Matches(long owed) =>
        new OwedDivisibleByRule(_settings).Matches(new Transaction(owed, paid: owed + 100, Usd));

    [Theory]
    [Trait("AC", "AC-004")]
    [InlineData(333)]
    [InlineData(300)]
    public void Matches_OwedCentsDivisibleByThree_IsTrue(long owed)
    {
        Matches(owed).ShouldBeTrue();
    }

    [Theory]
    [Trait("AC", "AC-004")]
    [InlineData(212)]
    [InlineData(197)]
    public void Matches_OwedCentsNotDivisibleByThree_IsFalse(long owed)
    {
        Matches(owed).ShouldBeFalse();
    }

    [Fact]
    [Trait("AC", "AC-013")]
    public void Matches_UsesTheActiveDivisor()
    {
        _settings.Change(5);

        Matches(500).ShouldBeTrue();
        Matches(333).ShouldBeFalse();
    }

    [Fact]
    public void Constructor_NullSettings_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new OwedDivisibleByRule(null!));
    }

    [Fact]
    public void Priority_IsTheDefaultPriority()
    {
        new OwedDivisibleByRule(_settings).Priority.ShouldBe(OwedDivisibleByRule.DefaultPriority);
    }

    [Fact]
    public void StrategyType_IsRandomChange()
    {
        new OwedDivisibleByRule(_settings).StrategyType.ShouldBe(typeof(RandomChangeStrategy));
    }
}
