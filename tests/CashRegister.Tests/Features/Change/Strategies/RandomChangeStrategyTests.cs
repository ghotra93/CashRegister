using CashRegister.Features.Change.Money;
using CashRegister.Features.Change.Strategies;
using CashRegister.Features.Currencies;

namespace CashRegister.Tests.Features.Change.Strategies;

public sealed class RandomChangeStrategyTests
{
    private static readonly UsdCurrency Usd = new();

    private static IReadOnlyList<ChangeLine> MakeChange(int seed, long amount) =>
        new RandomChangeStrategy(new Random(seed)).MakeChange(amount, Usd);

    private static string Describe(IReadOnlyList<ChangeLine> lines) =>
        string.Join(",", lines.Select(l => $"{l.Count} {l.Denomination.SingularName}"));

    [Fact]
    [Trait("AC", "AC-005")]
    public void MakeChange_ManySeedsAndAmounts_AlwaysSumsExactlyToAmount()
    {
        for (var seed = 0; seed < 1000; seed++)
        {
            var amount = seed % 501;
            MakeChange(seed, amount).Sum(l => l.Total).ShouldBe(amount, $"seed {seed}, amount {amount}");
        }
    }

    [Fact]
    public void MakeChange_SameSeed_GivesSameResult()
    {
        Describe(MakeChange(seed: 42, amount: 167)).ShouldBe(Describe(MakeChange(seed: 42, amount: 167)));
    }

    [Fact]
    [Trait("AC", "AC-004")]
    public void MakeChange_DifferentSeeds_ProduceDifferentDenominations()
    {
        var distinctResults = Enumerable.Range(0, 20)
            .Select(seed => Describe(MakeChange(seed, 167)))
            .Distinct()
            .Count();

        distinctResults.ShouldBeGreaterThan(1);
    }

    [Fact]
    [Trait("AC", "AC-007")]
    public void MakeChange_LinesAreLargestFirstWithoutZeroCounts()
    {
        for (var seed = 0; seed < 200; seed++)
        {
            var lines = MakeChange(seed, 467);

            lines.ShouldAllBe(l => l.Count > 0);
            lines.Select(l => l.Denomination.ValueInMinorUnits).ShouldBeInOrder(SortDirection.Descending);
        }
    }

    [Fact]
    public void MakeChange_Zero_IsEmpty()
    {
        MakeChange(seed: 1, amount: 0).ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_NullRandom_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new RandomChangeStrategy(null!));
    }

    [Fact]
    public void MakeChange_NegativeAmount_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => MakeChange(seed: 1, amount: -1));
    }
}
