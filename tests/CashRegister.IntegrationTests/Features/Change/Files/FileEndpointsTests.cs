using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace CashRegister.IntegrationTests.Features.Change.Files;

public sealed class FileEndpointsTests(CashRegisterApiFactory factory) : IClassFixture<CashRegisterApiFactory>
{
    private const string ReadmeSample = "2.12,3.00\n\n1.97,2.00\n\n3.33,5.00\n";
    private readonly HttpClient _client = factory.CreateClient();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private sealed record FileSummary(Guid Id, string FileName, DateTimeOffset UploadedAt, int LineCount, int ErrorLineCount);

    private async Task<HttpResponseMessage> Upload(string content, string fileName = "input.txt")
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", fileName);

        return await _client.PostAsync(new Uri("/api/files", UriKind.Relative), form, Cancellation);
    }

    private async Task<FileSummary> UploadOk(string content, string fileName = "input.txt")
    {
        var response = await Upload(content, fileName);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var summary = await response.Content.ReadFromJsonAsync<FileSummary>(Cancellation);
        summary.ShouldNotBeNull();
        return summary;
    }

    private Task<HttpResponseMessage> Download(Guid id) =>
        _client.GetAsync(new Uri($"/api/files/{id}/output", UriKind.Relative), Cancellation);

    private static async Task ShouldBeProblem(HttpResponseMessage response, HttpStatusCode status, string title)
    {
        response.StatusCode.ShouldBe(status);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation);
        problem.ShouldNotBeNull();
        problem.Title.ShouldBe(title);
    }

    [Fact]
    [Trait("AC", "AC-001")]
    [Trait("Category", "Integration")]
    public async Task Upload_ReadmeSample_Returns201WithOneLinePerTransaction()
    {
        var response = await Upload(ReadmeSample, "readme.txt");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var summary = await response.Content.ReadFromJsonAsync<FileSummary>(Cancellation);
        summary.ShouldNotBeNull();
        summary.FileName.ShouldBe("readme.txt");
        summary.LineCount.ShouldBe(3);
        summary.ErrorLineCount.ShouldBe(0);
        response.Headers.Location.ShouldBe(new Uri($"/api/files/{summary.Id}", UriKind.Relative));
    }

    [Fact]
    [Trait("AC", "AC-027")]
    [Trait("Category", "Integration")]
    public async Task List_AfterUpload_ContainsTheFileNewestFirst()
    {
        var first = await UploadOk("1.00,2.00\n", "first.txt");
        var second = await UploadOk("1.00,2.00\n", "second.txt");

        var files = await _client.GetFromJsonAsync<List<FileSummary>>(new Uri("/api/files", UriKind.Relative), Cancellation);

        files.ShouldNotBeNull();
        var ids = files.Select(f => f.Id).ToList();
        ids.ShouldContain(first.Id);
        ids.IndexOf(second.Id).ShouldBeLessThan(ids.IndexOf(first.Id));
    }

    [Fact]
    [Trait("AC", "AC-028")]
    [Trait("Category", "Integration")]
    public async Task Download_ReturnsThePlainTextOutputAsAnAttachment()
    {
        var summary = await UploadOk(ReadmeSample, "readme.txt");

        var response = await Download(summary.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/plain");
        response.Content.Headers.ContentDisposition?.DispositionType.ShouldBe("attachment");
        response.Content.Headers.ContentDisposition?.FileNameStar.ShouldBe("readme-change.txt");

        var lines = (await response.Content.ReadAsStringAsync(Cancellation)).Split('\n');
        lines.Length.ShouldBe(3);
        lines[0].ShouldBe("3 quarters,1 dime,3 pennies");
        lines[1].ShouldBe("3 pennies");
    }

    [Fact]
    [Trait("AC", "AC-023")]
    [Trait("Category", "Integration")]
    public async Task Upload_MoreThan1000Lines_Returns400TooManyLines()
    {
        var content = string.Concat(Enumerable.Repeat("1.00,2.00\n", 1001));

        await ShouldBeProblem(await Upload(content), HttpStatusCode.BadRequest, "Too many lines");
    }

    [Fact]
    [Trait("AC", "AC-015")]
    [Trait("Category", "Integration")]
    public async Task Upload_WithoutAFile_Returns400InvalidFile()
    {
        using var form = new MultipartFormDataContent { { new StringContent("x"), "notAFile" } };

        var response = await _client.PostAsync(new Uri("/api/files", UriKind.Relative), form, Cancellation);

        await ShouldBeProblem(response, HttpStatusCode.BadRequest, "Invalid file");
    }

    [Fact]
    [Trait("AC", "AC-015")]
    [Trait("Category", "Integration")]
    public async Task Download_UnknownId_Returns404FileNotFound()
    {
        await ShouldBeProblem(await Download(Guid.NewGuid()), HttpStatusCode.NotFound, "File not found");
    }

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("..\\..\\secret\\evil.txt", "evil.txt", "evil-change.txt")]
    [InlineData("../etc/passwd", "passwd", "passwd-change.txt")]
    public async Task Upload_PathInFileName_IsStrippedToTheBareName(string uploadedName, string storedName, string downloadName)
    {
        var summary = await UploadOk("1.00,2.00\n", uploadedName);

        summary.FileName.ShouldBe(storedName);
        (await Download(summary.Id)).Content.Headers.ContentDisposition?.FileNameStar.ShouldBe(downloadName);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Upload_NameWithNothingLeftAfterStrippingThePath_FallsBackToUploadTxt()
    {
        var summary = await UploadOk("1.00,2.00\n", "folder/");

        summary.FileName.ShouldBe("upload.txt");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Upload_VeryLongFileName_IsTruncatedTo100Characters()
    {
        var summary = await UploadOk("1.00,2.00\n", new string('a', 150) + ".txt");

        summary.FileName.Length.ShouldBe(100);
    }
}
