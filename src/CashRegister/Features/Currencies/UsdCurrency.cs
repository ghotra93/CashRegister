namespace CashRegister.Features.Currencies;

/// <summary>US dollar, the only currency supported in v1.</summary>
public sealed class UsdCurrency() : Currency(
    code: "USD",
    decimalSeparator: '.',
    minorUnitDigits: 2,
    denominations:
    [
        new("dollar", "dollars", 100),
        new("quarter", "quarters", 25),
        new("dime", "dimes", 10),
        new("nickel", "nickels", 5),
        new("penny", "pennies", 1),
    ]);
