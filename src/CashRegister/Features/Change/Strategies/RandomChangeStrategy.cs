using CashRegister.Features.Change.Money;
using CashRegister.Features.Currencies;

namespace CashRegister.Features.Change.Strategies;

/// <summary>
/// Random change behaviour (AC-004): walking from the largest denomination down, take a
/// random count of each that still fits. The smallest unit makes up whatever is left, so
/// the total is always exact (AC-005). The result may happen to be minimal (Q-007).
/// </summary>
/// <param name="random">Injected so tests can use a fixed seed (ADR-003).</param>
public sealed class RandomChangeStrategy(Random random) : IChangeStrategy
{
    private readonly Random _random = random ?? throw new ArgumentNullException(nameof(random));

    public IReadOnlyList<ChangeLine> MakeChange(long amount, Currency currency)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        ArgumentNullException.ThrowIfNull(currency);

        var lines = new List<ChangeLine>();
        var remaining = amount;
        var smallest = currency.Denominations[^1];

        foreach (var denomination in currency.Denominations)
        {
            var mostThatFit = remaining / denomination.ValueInMinorUnits;
            var count = denomination == smallest
                ? mostThatFit
                : _random.NextInt64(0, mostThatFit + 1);

            if (count == 0)
            {
                continue;
            }

            var line = new ChangeLine(denomination, count);
            lines.Add(line);
            remaining -= line.Total;
        }

        return lines;
    }
}
