using CashRegister.Features.Change.Money;
using CashRegister.Features.Currencies;

namespace CashRegister.Features.Change.Strategies;

/// <summary>Decides which denominations make up an amount of change.</summary>
public interface IChangeStrategy
{
    /// <returns>Lines ordered largest denomination first, with zero counts omitted.</returns>
    IReadOnlyList<ChangeLine> MakeChange(long amount, Currency currency);
}
