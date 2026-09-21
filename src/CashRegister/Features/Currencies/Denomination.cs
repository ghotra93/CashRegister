namespace CashRegister.Features.Currencies;

/// <summary>A physical note or coin, e.g. a quarter worth 25 cents.</summary>
public sealed record Denomination
{
    public Denomination(string singularName, string pluralName, long valueInMinorUnits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(singularName);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluralName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(valueInMinorUnits);

        SingularName = singularName;
        PluralName = pluralName;
        ValueInMinorUnits = valueInMinorUnits;
    }

    public string SingularName { get; }

    public string PluralName { get; }

    /// <summary>Value in the currency's smallest unit (cents for USD).</summary>
    public long ValueInMinorUnits { get; }

    public string NameFor(long count) => count == 1 ? SingularName : PluralName;
}
