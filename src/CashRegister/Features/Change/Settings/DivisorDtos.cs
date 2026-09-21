namespace CashRegister.Features.Change.Settings;

/// <summary>The active special-case divisor.</summary>
public sealed record DivisorResponse(int Divisor);

/// <summary>
/// Request to change the divisor. <see cref="Divisor"/> is nullable so a missing value
/// can be reported as "Invalid divisor" rather than silently read as 0.
/// </summary>
public sealed record ChangeDivisorRequest(int? Divisor);
