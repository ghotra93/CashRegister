using CashRegister.Features.Change.Money;

namespace CashRegister.Features.Change.Rules;

/// <summary>
/// A special case that overrides minimal change (DC-001). Add a new one by writing a new
/// sealed class and registering it; nothing existing changes.
/// </summary>
public interface IChangeRule
{
    /// <summary>When several rules match, the lowest value wins (AC-024).</summary>
    int Priority { get; }

    /// <summary>The <see cref="Strategies.IChangeStrategy"/> implementation to use when this rule matches.</summary>
    Type StrategyType { get; }

    bool Matches(Transaction transaction);
}
