using System.Globalization;
using CashRegister.Features.Change.Money;
using CashRegister.Features.Currencies;

namespace CashRegister.Features.Change.Parsing;

/// <summary>
/// Parses an <c>owed,paid</c> line into a <see cref="Transaction"/> in minor units.
/// Checks run in the order set by ADR-007: format, negative, decimal places, paid &lt; owed.
/// </summary>
internal static class TransactionLineParser
{
    private const char FieldSeparator = ',';
    private const char MinusSign = '-';

    public static LineParseResult Parse(string line, Currency currency)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(currency);

        var fields = line.Split(FieldSeparator);
        if (fields.Length != 2)
        {
            return LineParseResult.Failure(LineErrors.InvalidLine);
        }

        var owedAmount = ParsedAmount.From(fields[0], currency);
        var paidAmount = ParsedAmount.From(fields[1], currency);

        if (owedAmount is null || paidAmount is null)
        {
            return LineParseResult.Failure(LineErrors.InvalidLine);
        }

        if (owedAmount.IsNegative || paidAmount.IsNegative)
        {
            return LineParseResult.Failure(LineErrors.NegativeAmount);
        }

        if (owedAmount.DecimalPlaces > currency.MinorUnitDigits || paidAmount.DecimalPlaces > currency.MinorUnitDigits)
        {
            return LineParseResult.Failure(LineErrors.TooManyDecimalPlaces(currency.MinorUnitDigits));
        }

        var owed = owedAmount.ToMinorUnits(currency.MinorUnitDigits);
        var paid = paidAmount.ToMinorUnits(currency.MinorUnitDigits);

        if (owed is null || paid is null)
        {
            return LineParseResult.Failure(LineErrors.InvalidLine);
        }

        if (paid < owed)
        {
            return LineParseResult.Failure(LineErrors.PaidLessThanOwed);
        }

        return LineParseResult.Success(new Transaction(owed.Value, paid.Value, currency));
    }

    /// <summary>
    /// One amount split into its parts. Only digits, an optional leading minus and at most one
    /// currency decimal separator are accepted: no symbols or thousands separators (Q-013).
    /// </summary>
    private sealed record ParsedAmount(bool IsNegative, string WholeDigits, string FractionDigits)
    {
        public int DecimalPlaces => FractionDigits.Length;

        public static ParsedAmount? From(string field, Currency currency)
        {
            var text = field.Trim();

            var isNegative = text.StartsWith(MinusSign);
            if (isNegative)
            {
                text = text[1..];
            }

            var parts = text.Split(currency.DecimalSeparator);
            if (parts.Length > 2)
            {
                return null;
            }

            var wholeDigits = parts[0];
            if (!IsDigitsOnly(wholeDigits))
            {
                return null;
            }

            var hasSeparator = parts.Length == 2;
            if (!hasSeparator)
            {
                return new ParsedAmount(isNegative, wholeDigits, FractionDigits: string.Empty);
            }

            var fractionDigits = parts[1];
            if (!IsDigitsOnly(fractionDigits))
            {
                return null;
            }

            return new ParsedAmount(isNegative, wholeDigits, fractionDigits);
        }

        /// <returns>The amount in minor units, or <c>null</c> if it is too large to hold.</returns>
        public long? ToMinorUnits(int minorUnitDigits)
        {
            var digits = WholeDigits + FractionDigits.PadRight(minorUnitDigits, '0');
            if (!long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var minorUnits))
            {
                return null;
            }

            return minorUnits;
        }

        private static bool IsDigitsOnly(string text) => text.Length > 0 && text.All(char.IsAsciiDigit);
    }
}
