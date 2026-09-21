using CashRegister.Features.Change.Money;
using CashRegister.Features.Currencies;

namespace CashRegister.Features.Change.Strategies;

/// <summary>
/// Minimum physical change: take as many of each denomination as fit, largest first.
/// This greedy approach is optimal for canonical coin systems such as USD (AC-003).
/// </summary>
public sealed class MinimalChangeStrategy : IChangeStrategy
{
    public IReadOnlyList<ChangeLine> MakeChange(long amount, Currency currency)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        ArgumentNullException.ThrowIfNull(currency);

        var lines = new List<ChangeLine>();
        var remaining = amount;

        foreach (var denomination in currency.Denominations)
        {
            var count = remaining / denomination.ValueInMinorUnits;
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
