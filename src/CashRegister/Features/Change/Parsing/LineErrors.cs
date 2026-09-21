namespace CashRegister.Features.Change.Parsing;

/// <summary>Line error messages written to the output in place of change (ADR-007).</summary>
internal static class LineErrors
{
    public const string InvalidLine = "Error: invalid line, expected '<owed>,<paid>'";
    public const string NegativeAmount = "Error: amounts must not be negative";
    public const string PaidLessThanOwed = "Error: amount paid is less than amount owed";

    public static string TooManyDecimalPlaces(int maximumDigits) =>
        $"Error: amounts must have at most {maximumDigits} decimal places";
}
