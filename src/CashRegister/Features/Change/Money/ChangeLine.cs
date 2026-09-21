using CashRegister.Features.Currencies;

namespace CashRegister.Features.Change.Money;

/// <summary>A count of one denomination within the change, e.g. 3 quarters.</summary>
public sealed record ChangeLine(Denomination Denomination, long Count)
{
    public long Total => Denomination.ValueInMinorUnits * Count;
}
