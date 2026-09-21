namespace CashRegister.Features.Change.Rules;

/// <summary>The active special-case divisor, changeable from the UI (AC-025).</summary>
public interface IDivisorSettings
{
    int Current { get; }

    /// <exception cref="ArgumentOutOfRangeException">The divisor is less than 1 (AC-026).</exception>
    void Change(int divisor);
}
