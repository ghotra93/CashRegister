using CashRegister.Features.Change.Money;
using CashRegister.Features.Change.Strategies;

namespace CashRegister.Features.Change.Rules;

/// <summary>
/// The client's twist: when the amount owed in cents is divisible by the active
/// divisor, hand back random change (AC-004, AC-013).
/// </summary>
internal sealed class OwedDivisibleByRule(IDivisorSettings settings) : IChangeRule
{
    /// <summary>Leaves room for future rules to sit above or below this one.</summary>
    public const int DefaultPriority = 100;

    private readonly IDivisorSettings _settings = settings ?? throw new ArgumentNullException(nameof(settings));

    public int Priority => DefaultPriority;

    public Type StrategyType => typeof(RandomChangeStrategy);

    public bool Matches(Transaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        return transaction.Owed % _settings.Current == 0;
    }
}
