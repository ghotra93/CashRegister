using System.Text;
using CashRegister.Features.Change;
using CashRegister.Features.Change.Files;
using CashRegister.Features.Change.Processing;
using CashRegister.Features.Change.Rules;
using CashRegister.Features.Change.Strategies;
using CashRegister.Features.Currencies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Testing;

namespace CashRegister.Tests.Features.Change.Files;

/// <summary>Unit tests for the /api/files handlers, called directly without a host.</summary>
public sealed class FileEndpointsTests
{
    private static readonly DateTimeOffset UploadTime = new(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);

    private readonly InMemoryUploadedFileStore _store = new();
    private readonly ChangeFileProcessor _processor = new(
        new ChangeCalculator(
            [new OwedDivisibleByRule(new InMemoryDivisorSettings())],
            [new MinimalChangeStrategy(), new RandomChangeStrategy(new Random(1))]),
        new FakeLogger<ChangeFileProcessor>());
    private readonly ActiveCurrency _usd = new(new UsdCurrency());

    /// <summary>Builds (but never starts) an app with the module mapped, to read its route table.</summary>
    internal static class RouteTable
    {
        public static WebApplication BuildApp()
        {
            var builder = WebApplication.CreateBuilder();
            builder.Services.AddCashRegister(builder.Configuration);
            var app = builder.Build();
            app.MapCashRegister();
            return app;
        }

        public static List<string> Routes(WebApplication app) =>
        [
            .. ((IEndpointRouteBuilder)app).DataSources
                .SelectMany(source => source.Endpoints)
                .OfType<RouteEndpoint>()
                .SelectMany(endpoint =>
                    (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                    .Select(method => $"{method} {endpoint.RoutePattern.RawText}")),
        ];
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static FormFile TextFile(string content, string fileName = "input.txt")
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", fileName);
    }

    private async Task<IResult> Upload(IFormFile? file) =>
        (await FileEndpoints.UploadAsync(
            file, _processor, _usd, _store, new FixedTimeProvider(UploadTime), TestContext.Current.CancellationToken)).Result;

    [Fact]
    [Trait("AC", "AC-015")]
    public async Task Upload_WithoutAFile_ReturnsInvalidFileProblem()
    {
        var problem = (await Upload(null)).ShouldBeOfType<ProblemHttpResult>();

        problem.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        problem.ProblemDetails.Title.ShouldBe("Invalid file");
    }

    [Fact]
    [Trait("AC", "AC-023")]
    public async Task Upload_MoreThan1000Lines_ReturnsTooManyLinesProblemAndStoresNothing()
    {
        var content = string.Concat(Enumerable.Repeat("1.00,2.00\n", ChangeFileProcessor.MaxLines + 1));

        var problem = (await Upload(TextFile(content))).ShouldBeOfType<ProblemHttpResult>();

        problem.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        problem.ProblemDetails.Title.ShouldBe("Too many lines");
        _store.ListNewestFirst().ShouldBeEmpty();
    }

    [Fact]
    [Trait("AC", "AC-027")]
    public async Task Upload_ValidFile_StoresItAtTheCurrentTimeAndReturnsCreated()
    {
        var created = (await Upload(TextFile("2.12,3.00\nbad\n1.97,2.00\n"))).ShouldBeOfType<Created<UploadedFileSummary>>();

        var summary = created.Value.ShouldNotBeNull();
        summary.FileName.ShouldBe("input.txt");
        summary.UploadedAt.ShouldBe(UploadTime);
        summary.LineCount.ShouldBe(3);
        summary.ErrorLineCount.ShouldBe(1);
        created.Location.ShouldBe($"/api/files/{summary.Id}");
        _store.TryGet(summary.Id, out var stored).ShouldBeTrue();
        stored.OutputText.ShouldStartWith("3 quarters,1 dime,3 pennies\n");
    }

    [Theory]
    [InlineData("..\\..\\secret\\evil.txt", "evil.txt")]
    [InlineData("../etc/passwd", "passwd")]
    [InlineData("folder/", "upload.txt")]
    [InlineData("  spaced.txt  ", "spaced.txt")]
    public async Task Upload_FileNameWithAPath_KeepsOnlyTheSafeBareName(string uploadedName, string storedName)
    {
        var created = (await Upload(TextFile("1.00,2.00\n", uploadedName))).ShouldBeOfType<Created<UploadedFileSummary>>();

        created.Value!.FileName.ShouldBe(storedName);
    }

    [Fact]
    public async Task Upload_VeryLongFileName_IsCutTo100Characters()
    {
        var created = (await Upload(TextFile("1.00,2.00\n", new string('a', 150) + ".txt"))).ShouldBeOfType<Created<UploadedFileSummary>>();

        created.Value!.FileName.Length.ShouldBe(100);
    }

    [Fact]
    [Trait("AC", "AC-027")]
    public void List_ReturnsSummariesNewestFirst()
    {
        _store.Add(new UploadedFile(Guid.NewGuid(), "old.txt", UploadTime, 1, 0, "3 pennies"));
        _store.Add(new UploadedFile(Guid.NewGuid(), "new.txt", UploadTime.AddHours(1), 2, 1, "3 pennies\nError"));

        var files = FileEndpoints.List(_store).Value.ShouldNotBeNull();

        files.Select(f => f.FileName).ShouldBe(["new.txt", "old.txt"]);
        files[0].ErrorLineCount.ShouldBe(1);
    }

    [Fact]
    [Trait("AC", "AC-028")]
    public void Download_KnownId_ReturnsThePlainTextOutputAsAChangeFile()
    {
        var file = new UploadedFile(Guid.NewGuid(), "monday.csv", UploadTime, 1, 0, "3 pennies");
        _store.Add(file);

        var download = FileEndpoints.Download(file.Id, _store).Result.ShouldBeOfType<FileContentHttpResult>();

        download.ContentType.ShouldBe("text/plain; charset=utf-8");
        download.FileDownloadName.ShouldBe("monday-change.txt");
        Encoding.UTF8.GetString(download.FileContents.Span).ShouldBe("3 pennies");
    }

    [Fact]
    [Trait("AC", "AC-027")]
    public async Task MapCashRegister_RegistersTheFileRoutes()
    {
        await using var app = RouteTable.BuildApp();

        string[] expected = ["POST /api/files/", "GET /api/files/", "GET /api/files/{id:guid}/output"];

        expected.ShouldBeSubsetOf(RouteTable.Routes(app));
    }

    [Fact]
    [Trait("AC", "AC-015")]
    public void Download_UnknownId_ReturnsFileNotFoundProblem()
    {
        var problem = FileEndpoints.Download(Guid.NewGuid(), _store).Result.ShouldBeOfType<ProblemHttpResult>();

        problem.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        problem.ProblemDetails.Title.ShouldBe("File not found");
    }
}
