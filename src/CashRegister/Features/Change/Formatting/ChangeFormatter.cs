using CashRegister.Features.Change.Money;

namespace CashRegister.Features.Change.Formatting;

/// <summary>
/// Renders change as one output line, e.g. <c>3 quarters,1 dime,3 pennies</c> (AC-006).
/// </summary>
internal static class ChangeFormatter
{
    /// <summary>Output when the amount paid equals the amount owed (AC-016).</summary>
    public const string NoChange = "No change";

    private const string EntrySeparator = ",";

    public static string Format(IReadOnlyList<ChangeLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var entries = lines
            .Where(line => line.Count > 0)                                   // AC-008
            .OrderByDescending(line => line.Denomination.ValueInMinorUnits)  // AC-007
            .Select(line => $"{line.Count} {line.Denomination.NameFor(line.Count)}") // AC-009, AC-010
            .ToList();

        if (entries.Count == 0)
        {
            return NoChange;
        }

        return string.Join(EntrySeparator, entries);
    }
}
