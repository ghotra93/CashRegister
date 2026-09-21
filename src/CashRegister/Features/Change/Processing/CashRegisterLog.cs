using Microsoft.Extensions.Logging;

namespace CashRegister.Features.Change.Processing;

/// <summary>Source-generated structured log events (NFR-002, ADR-008). File contents are never logged.</summary>
internal static partial class CashRegisterLog
{
    [LoggerMessage(
        EventId = 1,
        EventName = "FileProcessed",
        Level = LogLevel.Information,
        Message = "Processed {FileName}: {LineCount} lines, {ErrorLineCount} with errors, in {ElapsedMs} ms")]
    public static partial void FileProcessed(this ILogger logger, string fileName, int lineCount, int errorLineCount, long elapsedMs);

    [LoggerMessage(
        EventId = 2,
        EventName = "FileRejected",
        Level = LogLevel.Warning,
        Message = "Rejected {FileName}: {Reason}")]
    public static partial void FileRejected(this ILogger logger, string fileName, string reason);

    [LoggerMessage(
        EventId = 3,
        EventName = "DivisorChanged",
        Level = LogLevel.Information,
        Message = "Special-case divisor changed from {OldDivisor} to {NewDivisor}")]
    public static partial void DivisorChanged(this ILogger logger, int oldDivisor, int newDivisor);
}
