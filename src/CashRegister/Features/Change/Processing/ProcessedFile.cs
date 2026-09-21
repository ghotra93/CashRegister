namespace CashRegister.Features.Change.Processing;

/// <summary>The outcome of processing one uploaded transaction file.</summary>
internal sealed record ProcessedFile
{
    /// <summary>Output lines are joined with LF and no trailing newline (ADR-007).</summary>
    public const string LineSeparator = "\n";

    private ProcessedFile(bool exceededLineLimit, IReadOnlyList<string> outputLines, int errorLineCount)
    {
        ExceededLineLimit = exceededLineLimit;
        OutputLines = outputLines;
        ErrorLineCount = errorLineCount;
    }

    /// <summary>True when the file had more non-blank lines than allowed (AC-023); no output is produced.</summary>
    public bool ExceededLineLimit { get; }

    /// <summary>One entry per non-blank input line, in input order (AC-001, AC-002).</summary>
    public IReadOnlyList<string> OutputLines { get; }

    public int ErrorLineCount { get; }

    public int LineCount => OutputLines.Count;

    public string OutputText => string.Join(LineSeparator, OutputLines);

    public static ProcessedFile Completed(IReadOnlyList<string> outputLines, int errorLineCount) =>
        new(exceededLineLimit: false, outputLines, errorLineCount);

    public static ProcessedFile TooManyLines() =>
        new(exceededLineLimit: true, outputLines: [], errorLineCount: 0);
}
