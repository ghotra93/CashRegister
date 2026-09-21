namespace CashRegister.Features.Change.Rules;

/// <summary>Holds the divisor in process memory; lost on restart (NFR-005, ADR-002).</summary>
public sealed class InMemoryDivisorSettings : IDivisorSettings
{
    public const int DefaultDivisor = 3;
    public const int MinimumDivisor = 1;

    private int _current = DefaultDivisor;

    public int Current => Volatile.Read(ref _current);

    public void Change(int divisor)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(divisor, MinimumDivisor);
        Volatile.Write(ref _current, divisor);
    }
}
