using CashRegister.Features.Change.Money;

namespace CashRegister.Features.Change.Parsing;

/// <summary>Either a parsed <see cref="Transaction"/> or a line error message, never both.</summary>
internal sealed record LineParseResult
{
    private LineParseResult(Transaction? transaction, string? error)
    {
        Transaction = transaction;
        Error = error;
    }

    public Transaction? Transaction { get; }

    public string? Error { get; }

    public static LineParseResult Success(Transaction transaction) => new(transaction, error: null);

    public static LineParseResult Failure(string error) => new(transaction: null, error);
}
