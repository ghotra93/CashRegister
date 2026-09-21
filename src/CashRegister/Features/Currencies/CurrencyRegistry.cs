using System.Diagnostics.CodeAnalysis;

namespace CashRegister.Features.Currencies;

/// <summary>Looks up registered currencies by code, ignoring case.</summary>
public sealed class CurrencyRegistry : ICurrencyRegistry
{
    private readonly Dictionary<string, Currency> _byCode;

    public CurrencyRegistry(IEnumerable<Currency> currencies)
    {
        ArgumentNullException.ThrowIfNull(currencies);

        // ToDictionary throws ArgumentException on a duplicate code.
        _byCode = currencies.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<Currency> All => _byCode.Values;

    public bool TryGet(string code, [MaybeNullWhen(false)] out Currency currency)
    {
        ArgumentNullException.ThrowIfNull(code);
        return _byCode.TryGetValue(code, out currency);
    }
}
