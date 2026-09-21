using System.Diagnostics.CodeAnalysis;

namespace CashRegister.Features.Currencies;

public interface ICurrencyRegistry
{
    IReadOnlyCollection<Currency> All { get; }

    bool TryGet(string code, [MaybeNullWhen(false)] out Currency currency);
}
