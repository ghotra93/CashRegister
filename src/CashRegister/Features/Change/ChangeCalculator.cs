using CashRegister.Features.Change.Money;
using CashRegister.Features.Change.Rules;
using CashRegister.Features.Change.Strategies;

namespace CashRegister.Features.Change;

/// <summary>
/// Picks how to make change: the matching rule with the lowest priority value decides the
/// strategy (AC-024); with no match, minimal change is used (AC-003).
/// </summary>
internal sealed class ChangeCalculator
{
    private readonly List<IChangeRule> _rulesByPriority;
    private readonly Dictionary<Type, IChangeStrategy> _strategiesByType;
    private readonly IChangeStrategy _minimalChange;

    public ChangeCalculator(IEnumerable<IChangeRule> rules, IEnumerable<IChangeStrategy> strategies)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(strategies);

        // OrderBy is stable, so rules with equal priority keep their registration order.
        _rulesByPriority = [.. rules.OrderBy(r => r.Priority)];
        _strategiesByType = strategies.ToDictionary(s => s.GetType());
        _minimalChange = StrategyOfType(typeof(MinimalChangeStrategy));

        // Fail at startup rather than on the first matching transaction.
        foreach (var rule in _rulesByPriority)
        {
            StrategyOfType(rule.StrategyType);
        }
    }

    public IReadOnlyList<ChangeLine> Calculate(Transaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        var matchingRule = _rulesByPriority.FirstOrDefault(r => r.Matches(transaction));
        var strategy = matchingRule is null ? _minimalChange : _strategiesByType[matchingRule.StrategyType];

        return strategy.MakeChange(transaction.ChangeDue, transaction.Currency);
    }

    private IChangeStrategy StrategyOfType(Type strategyType) =>
        _strategiesByType.TryGetValue(strategyType, out var strategy)
            ? strategy
            : throw new InvalidOperationException($"No change strategy of type {strategyType.Name} is registered.");
}
