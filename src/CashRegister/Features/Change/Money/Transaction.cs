using CashRegister.Features.Currencies;

namespace CashRegister.Features.Change.Money;

/// <summary>One sale from an input line, with amounts in the currency's minor units (cents).</summary>
public sealed record Transaction
{
    public Transaction(long owed, long paid, Currency currency)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(owed);
        ArgumentOutOfRangeException.ThrowIfNegative(paid);
        ArgumentNullException.ThrowIfNull(currency);

        if (paid < owed)
        {
            throw new ArgumentException("Amount paid is less than amount owed.", nameof(paid));
        }

        Owed = owed;
        Paid = paid;
        Currency = currency;
    }

    public long Owed { get; }

    public long Paid { get; }

    public Currency Currency { get; }

    public long ChangeDue => Paid - Owed;
}
