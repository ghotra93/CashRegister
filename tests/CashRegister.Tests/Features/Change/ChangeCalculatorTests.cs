using CashRegister.Features.Change;
using CashRegister.Features.Change.Money;
using CashRegister.Features.Change.Rules;
using CashRegister.Features.Change.Strategies;
using CashRegister.Features.Currencies;

namespace CashRegister.Tests.Features.Change;

public sealed class ChangeCalculatorTests
{
    private static readonly UsdCurrency Usd = new();
    private static readonly Denomination Marker = new("marker", "markers", 1);

    /// <summary>Strategy double that tags its output so a test can see which strategy ran.</summary>
    private abstract class TaggingStrategy(long tag) : IChangeStrategy
    {
        public IReadOnlyList<ChangeLine> MakeChange(long amount, Currency currency) => [new(Marker, tag)];
    }

    private sealed class FirstSpecialStrategy() : TaggingStrategy(1);

    private sealed class SecondSpecialStrategy() : TaggingStrategy(2);

    private sealed class AlwaysRule(int priority, Type strategyType) : IChangeRule
    {
        public int Priority => priority;

        public Type StrategyType => strategyType;

        public bool Matches(Transaction transaction) => true;
    }

    private static readonly IChangeStrategy[] AllStrategies =
    [
        new MinimalChangeStrategy(),
        new RandomChangeStrategy(new Random(7)),
        new FirstSpecialStrategy(),
        new SecondSpecialStrategy(),
    ];

    private static Transaction Sale(long owed, long paid) => new(owed, paid, Usd);

    [Fact]
    [Trait("AC", "AC-003")]
    public void Calculate_NoRuleMatches_UsesMinimalChange()
    {
        var calculator = new ChangeCalculator([new OwedDivisibleByRule(new InMemoryDivisorSettings())], AllStrategies);

        var lines = calculator.Calculate(Sale(owed: 212, paid: 300));

        lines.Select(l => (l.Denomination.SingularName, l.Count))
            .ShouldBe([("quarter", 3L), ("dime", 1L), ("penny", 3L)]);
    }

    [Fact]
    [Trait("AC", "AC-004")]
    public void Calculate_OwedDivisibleByDivisor_UsesRandomChange()
    {
        var calculator = new ChangeCalculator([new OwedDivisibleByRule(new InMemoryDivisorSettings())], AllStrategies);
        var expected = new RandomChangeStrategy(new Random(7)).MakeChange(167, Usd);

        var lines = calculator.Calculate(Sale(owed: 333, paid: 500));

        lines.ShouldBe(expected);
    }

    [Fact]
    [Trait("AC", "AC-024")]
    public void Calculate_SeveralRulesMatch_LowestPriorityValueWins()
    {
        var calculator = new ChangeCalculator(
            [
                new AlwaysRule(priority: 20, typeof(SecondSpecialStrategy)),
                new AlwaysRule(priority: 10, typeof(FirstSpecialStrategy)),
            ],
            AllStrategies);

        calculator.Calculate(Sale(owed: 100, paid: 200)).Single().Count.ShouldBe(1);
    }

    [Fact]
    [Trait("AC", "AC-024")]
    public void Calculate_SamePriority_FirstRegisteredWins()
    {
        var calculator = new ChangeCalculator(
            [
                new AlwaysRule(priority: 10, typeof(SecondSpecialStrategy)),
                new AlwaysRule(priority: 10, typeof(FirstSpecialStrategy)),
            ],
            AllStrategies);

        calculator.Calculate(Sale(owed: 100, paid: 200)).Single().Count.ShouldBe(2);
    }

    [Fact]
    [Trait("AC", "AC-025")]
    public void Calculate_DivisorChanged_AppliesToNextCalculation()
    {
        var settings = new InMemoryDivisorSettings();
        var calculator = new ChangeCalculator(
            [new AlwaysRuleWhenDivisible(settings)],
            AllStrategies);

        calculator.Calculate(Sale(owed: 500, paid: 600)).Single().Denomination.ShouldNotBe(Marker);

        settings.Change(5);

        calculator.Calculate(Sale(owed: 500, paid: 600)).Single().Denomination.ShouldBe(Marker);
    }

    /// <summary>Divisor-driven rule routed to a tagging strategy so the switch is observable.</summary>
    private sealed class AlwaysRuleWhenDivisible(IDivisorSettings settings) : IChangeRule
    {
        private readonly OwedDivisibleByRule _inner = new(settings);

        public int Priority => _inner.Priority;

        public Type StrategyType => typeof(FirstSpecialStrategy);

        public bool Matches(Transaction transaction) => _inner.Matches(transaction);
    }

    [Fact]
    public void Constructor_RuleWithUnregisteredStrategy_Throws()
    {
        Should.Throw<InvalidOperationException>(() => new ChangeCalculator(
            [new AlwaysRule(priority: 1, typeof(FirstSpecialStrategy))],
            [new MinimalChangeStrategy()]));
    }

    [Fact]
    public void Constructor_WithoutMinimalStrategy_Throws()
    {
        Should.Throw<InvalidOperationException>(() => new ChangeCalculator([], [new FirstSpecialStrategy()]));
    }
}
