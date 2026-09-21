namespace CashRegister.Features.Currencies;

/// <summary>
/// A monetary system and the denominations a cashier can hand back.
/// A new currency is added by subclassing and registering it (DC-002, ADR-004).
/// </summary>
public abstract class Currency
{
    protected Currency(string code, char decimalSeparator, int minorUnitDigits, IEnumerable<Denomination> denominations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentOutOfRangeException.ThrowIfNegative(minorUnitDigits);
        ArgumentNullException.ThrowIfNull(denominations);

        var largestFirst = denominations.OrderByDescending(d => d.ValueInMinorUnits).ToList();

        if (largestFirst.Count == 0)
        {
            throw new ArgumentException("A currency needs at least one denomination.", nameof(denominations));
        }

        if (largestFirst.DistinctBy(d => d.ValueInMinorUnits).Count() != largestFirst.Count)
        {
            throw new ArgumentException("Denomination values must be unique.", nameof(denominations));
        }

        // Any amount must be payable exactly, so the smallest unit has to exist.
        if (largestFirst[^1].ValueInMinorUnits != 1)
        {
            throw new ArgumentException("A currency must include a denomination worth one minor unit.", nameof(denominations));
        }

        Code = code;
        DecimalSeparator = decimalSeparator;
        MinorUnitDigits = minorUnitDigits;
        Denominations = largestFirst.AsReadOnly();
    }

    /// <summary>ISO 4217 code, e.g. <c>USD</c>.</summary>
    public string Code { get; }

    /// <summary>Separator between major and minor units in input amounts (AC-029).</summary>
    public char DecimalSeparator { get; }

    /// <summary>Maximum decimal places an input amount may have.</summary>
    public int MinorUnitDigits { get; }

    /// <summary>Denominations ordered from largest to smallest value.</summary>
    public IReadOnlyList<Denomination> Denominations { get; }
}
