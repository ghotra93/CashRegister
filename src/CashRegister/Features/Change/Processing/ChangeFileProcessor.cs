using System.Diagnostics;
using CashRegister.Features.Change.Formatting;
using CashRegister.Features.Change.Parsing;
using CashRegister.Features.Currencies;
using Microsoft.Extensions.Logging;

namespace CashRegister.Features.Change.Processing;

/// <summary>
/// Turns an uploaded transaction file into one output line per non-blank input line:
/// the formatted change, or a line error message when that line is invalid.
/// </summary>
internal sealed class ChangeFileProcessor(ChangeCalculator calculator, ILogger<ChangeFileProcessor> logger)
{
    /// <summary>Most non-blank lines a file may contain (AC-023, Q-011).</summary>
    public const int MaxLines = 1000;

    private readonly ChangeCalculator _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
    private readonly ILogger<ChangeFileProcessor> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<ProcessedFile> ProcessAsync(
        Stream input,
        string fileName,
        Currency currency,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(currency);

        var startedAt = Stopwatch.GetTimestamp();
        var outputLines = new List<string>();
        var errorLineCount = 0;

        using var reader = new StreamReader(input);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue; // AC-022
            }

            if (outputLines.Count == MaxLines)
            {
                _logger.FileRejected(fileName, $"more than {MaxLines} lines");
                return ProcessedFile.TooManyLines();
            }

            var parsed = TransactionLineParser.Parse(line, currency);
            if (parsed.Transaction is { } transaction)
            {
                outputLines.Add(ChangeFormatter.Format(_calculator.Calculate(transaction)));
            }
            else
            {
                // A parse result without a transaction always carries an error.
                outputLines.Add(parsed.Error!);
                errorLineCount++; // AC-021: record the error and keep going
            }
        }

        var elapsedMs = (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        _logger.FileProcessed(fileName, outputLines.Count, errorLineCount, elapsedMs);

        return ProcessedFile.Completed(outputLines, errorLineCount);
    }
}
