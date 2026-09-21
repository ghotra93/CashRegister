using System.Diagnostics;
using System.Net;
using System.Text;

namespace CashRegister.IntegrationTests.Performance;

/// <summary>
/// NFR-001: processing a 1000-line file completes with p95 latency under 500 ms.
/// Measured in-process through the real endpoint (upload → process → store), after warm-up.
/// </summary>
public sealed class FileUploadPerformanceTests(CashRegisterApiFactory factory) : IClassFixture<CashRegisterApiFactory>
{
    private const int MaxLines = 1000;
    private const int WarmUpRuns = 5;
    private const int MeasuredRuns = 50;
    private static readonly TimeSpan P95Budget = TimeSpan.FromMilliseconds(500);

    private readonly HttpClient _client = factory.CreateClient();

    /// <summary>A realistic mix: minimal change, random change (owed divisible by 3) and error lines.</summary>
    private static readonly byte[] ThousandLineFile = Encoding.UTF8.GetBytes(string.Concat(
        Enumerable.Range(0, MaxLines).Select(i => (i % 10) switch
        {
            0 => "3.33,5.00\n",
            1 => "not a line\n",
            _ => $"{i % 97}.{i % 100:00},{100 + (i % 97)}.00\n",
        })));

    private async Task<TimeSpan> TimeOneUpload()
    {
        using var form = new MultipartFormDataContent { { new ByteArrayContent(ThousandLineFile), "file", "perf.txt" } };

        var startedAt = Stopwatch.GetTimestamp();
        var response = await _client.PostAsync(new Uri("/api/files", UriKind.Relative), form, TestContext.Current.CancellationToken);
        var elapsed = Stopwatch.GetElapsedTime(startedAt);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return elapsed;
    }

    [Fact]
    [Trait("AC", "AC-001")]
    [Trait("Category", "Integration")]
    [Trait("NFR", "NFR-001")]
    public async Task Upload_1000LineFile_P95IsUnder500Milliseconds()
    {
        for (var i = 0; i < WarmUpRuns; i++)
        {
            await TimeOneUpload();
        }

        var timings = new List<TimeSpan>();
        for (var i = 0; i < MeasuredRuns; i++)
        {
            timings.Add(await TimeOneUpload());
        }

        var p95 = timings.Order().ElementAt((int)Math.Ceiling(MeasuredRuns * 0.95) - 1);
        TestContext.Current.SendDiagnosticMessage($"1000-line upload p95 = {p95.TotalMilliseconds:F1} ms");

        p95.ShouldBeLessThan(P95Budget);
    }
}
