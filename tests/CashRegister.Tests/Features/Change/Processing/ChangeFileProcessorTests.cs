using System.Text;
using CashRegister.Features.Change;
using CashRegister.Features.Change.Parsing;
using CashRegister.Features.Change.Processing;
using CashRegister.Features.Change.Rules;
using CashRegister.Features.Change.Strategies;
using CashRegister.Features.Currencies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace CashRegister.Tests.Features.Change.Processing;

public sealed class ChangeFileProcessorTests
{
    /// <summary>The sample input from the README, including its blank separator lines.</summary>
    private const string ReadmeSample = "2.12,3.00\n\n1.97,2.00\n\n3.33,5.00\n";

    private static readonly UsdCurrency Usd = new();
    private readonly FakeLogger<ChangeFileProcessor> _logger = new();
    private readonly ChangeFileProcessor _processor;

    public ChangeFileProcessorTests()
    {
        var calculator = new ChangeCalculator(
            [new OwedDivisibleByRule(new InMemoryDivisorSettings())],
            [new MinimalChangeStrategy(), new RandomChangeStrategy(new Random(3))]);

        _processor = new ChangeFileProcessor(calculator, _logger);
    }

    private Task<ProcessedFile> Process(string content) =>
        _processor.ProcessAsync(
            new MemoryStream(Encoding.UTF8.GetBytes(content)),
            "input.txt",
            Usd,
            TestContext.Current.CancellationToken);

    [Fact]
    [Trait("AC", "AC-001")]
    public async Task Process_ReadmeSample_ProducesOneOutputLinePerTransaction()
    {
        var result = await Process(ReadmeSample);

        result.OutputLines.Count.ShouldBe(3);
    }

    [Fact]
    [Trait("AC", "AC-022")]
    public async Task Process_BlankAndWhitespaceLines_AreIgnored()
    {
        var result = await Process("\n   \n2.12,3.00\r\n\t\n1.97,2.00\n\n");

        result.OutputLines.Count.ShouldBe(2);
        result.LineCount.ShouldBe(2);
    }

    [Fact]
    [Trait("AC", "AC-002")]
    public async Task Process_OutputLines_KeepTheInputOrder()
    {
        var result = await Process("1.97,2.00\n2.12,3.00\n");

        result.OutputLines.ShouldBe(["3 pennies", "3 quarters,1 dime,3 pennies"]);
    }

    [Fact]
    [Trait("AC", "AC-011")]
    public async Task Process_ReadmeSample_FirstLineMatchesReadme()
    {
        (await Process(ReadmeSample)).OutputLines[0].ShouldBe("3 quarters,1 dime,3 pennies");
    }

    [Fact]
    [Trait("AC", "AC-012")]
    public async Task Process_ReadmeSample_SecondLineMatchesReadme()
    {
        (await Process(ReadmeSample)).OutputLines[1].ShouldBe("3 pennies");
    }

    [Fact]
    [Trait("AC", "AC-021")]
    public async Task Process_InvalidMiddleLine_WritesErrorAndContinues()
    {
        var result = await Process("2.12,3.00\nnot a line\n1.97,2.00\n");

        result.OutputLines.ShouldBe(["3 quarters,1 dime,3 pennies", LineErrors.InvalidLine, "3 pennies"]);
        result.ErrorLineCount.ShouldBe(1);
    }

    [Fact]
    public async Task Process_ExactPayment_WritesNoChange()
    {
        (await Process("2.00,2.00\n")).OutputLines.ShouldBe(["No change"]);
    }

    [Fact]
    [Trait("AC", "AC-023")]
    public async Task Process_ExactlyTheLineLimit_IsProcessed()
    {
        var content = string.Concat(Enumerable.Repeat("1.00,2.00\n", ChangeFileProcessor.MaxLines));

        var result = await Process(content);

        result.ExceededLineLimit.ShouldBeFalse();
        result.OutputLines.Count.ShouldBe(ChangeFileProcessor.MaxLines);
    }

    [Fact]
    [Trait("AC", "AC-023")]
    public async Task Process_OneLineOverTheLimit_IsRejected()
    {
        var content = string.Concat(Enumerable.Repeat("1.00,2.00\n", ChangeFileProcessor.MaxLines + 1));

        var result = await Process(content);

        result.ExceededLineLimit.ShouldBeTrue();
        result.OutputLines.ShouldBeEmpty();
    }

    [Fact]
    public async Task Process_EmptyFile_ProducesNoLines()
    {
        var result = await Process(string.Empty);

        result.ExceededLineLimit.ShouldBeFalse();
        result.OutputLines.ShouldBeEmpty();
        result.OutputText.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task OutputText_JoinsLinesWithLineFeedAndNoTrailingNewline()
    {
        (await Process("2.12,3.00\n1.97,2.00\n")).OutputText.ShouldBe("3 quarters,1 dime,3 pennies\n3 pennies");
    }

    [Fact]
    public async Task Process_Completed_LogsFileProcessedWithCounts()
    {
        await Process("2.12,3.00\nbad\n");

        var record = _logger.Collector.LatestRecord;
        record.Level.ShouldBe(LogLevel.Information);
        record.Id.Name.ShouldBe("FileProcessed");
        record.StructuredState.ShouldNotBeNull();
        record.StructuredState.ShouldContain(new KeyValuePair<string, string?>("FileName", "input.txt"));
        record.StructuredState.ShouldContain(new KeyValuePair<string, string?>("LineCount", "2"));
        record.StructuredState.ShouldContain(new KeyValuePair<string, string?>("ErrorLineCount", "1"));
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        var calculator = new ChangeCalculator([], [new MinimalChangeStrategy()]);

        Should.Throw<ArgumentNullException>(() => new ChangeFileProcessor(null!, _logger));
        Should.Throw<ArgumentNullException>(() => new ChangeFileProcessor(calculator, null!));
    }

    [Fact]
    public async Task Process_OverTheLimit_LogsFileRejected()
    {
        await Process(string.Concat(Enumerable.Repeat("1.00,2.00\n", ChangeFileProcessor.MaxLines + 1)));

        var record = _logger.Collector.LatestRecord;
        record.Level.ShouldBe(LogLevel.Warning);
        record.Id.Name.ShouldBe("FileRejected");
    }
}
