using CashRegister.Features.Currencies;
using Microsoft.Extensions.Options;

namespace CashRegister;

/// <summary>Settings bound from the <c>CashRegister</c> configuration section.</summary>
public sealed class CashRegisterOptions
{
    public const string SectionName = "CashRegister";

    /// <summary>Currency code that uploaded files are processed in; v1 supports USD only (ADR-004).</summary>
    public string Currency { get; set; } = "USD";
}

/// <summary>The currency uploaded files are processed in, resolved once from <see cref="CashRegisterOptions"/>.</summary>
public sealed record ActiveCurrency(Currency Currency);

/// <summary>Fails startup when the configured currency is not registered (ADR-004).</summary>
internal sealed class CashRegisterOptionsValidator(ICurrencyRegistry registry) : IValidateOptions<CashRegisterOptions>
{
    public ValidateOptionsResult Validate(string? name, CashRegisterOptions options)
    {
        if (registry.TryGet(options.Currency, out _))
        {
            return ValidateOptionsResult.Success;
        }

        var registered = string.Join(", ", registry.All.Select(c => c.Code));
        return ValidateOptionsResult.Fail(
            $"CashRegister:Currency '{options.Currency}' is not a registered currency. Registered: {registered}.");
    }
}
