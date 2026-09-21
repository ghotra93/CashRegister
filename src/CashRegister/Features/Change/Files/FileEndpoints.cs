using System.Text;
using CashRegister.Features.Change.Processing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace CashRegister.Features.Change.Files;

/// <summary><c>/api/files</c>: upload a transaction file, list uploads, download a file's change output.</summary>
internal static class FileEndpoints
{
    /// <summary>Upload size cap (ADR-005). 1000 lines fit easily within it.</summary>
    public const long MaxUploadBytes = 1024 * 1024;

    private const int MaxFileNameLength = 100;
    private const string FallbackFileName = "upload.txt";
    private const string DownloadSuffix = "-change.txt";
    private const string PlainTextUtf8 = "text/plain; charset=utf-8";

    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    public static IEndpointRouteBuilder MapFileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var files = endpoints.MapGroup("/api/files").WithTags("Files");

        files.MapPost("/", UploadAsync)
            .DisableAntiforgery() // v1 has no auth or cookies to protect (ADR-005)
            .WithFormOptions(multipartBodyLengthLimit: MaxUploadBytes);

        files.MapGet("/", List);
        files.MapGet("/{id:guid}/output", Download);

        return endpoints;
    }

    // Handlers are internal (not private) so unit tests can call them directly (T-017, ADR-009).
    internal static async Task<Results<Created<UploadedFileSummary>, ProblemHttpResult>> UploadAsync(
        IFormFile? file,
        ChangeFileProcessor processor,
        ActiveCurrency activeCurrency,
        IUploadedFileStore store,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return TypedResults.Problem(
                title: "Invalid file",
                detail: "Send the transaction file as multipart/form-data in a field named 'file'.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var fileName = SafeFileName(file.FileName);

        await using var content = file.OpenReadStream();
        var processed = await processor.ProcessAsync(content, fileName, activeCurrency.Currency, cancellationToken);

        if (processed.ExceededLineLimit)
        {
            return TypedResults.Problem(
                title: "Too many lines",
                detail: $"A file may contain at most {ChangeFileProcessor.MaxLines} non-blank lines.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var uploaded = new UploadedFile(
            Guid.NewGuid(),
            fileName,
            timeProvider.GetUtcNow(),
            processed.LineCount,
            processed.ErrorLineCount,
            processed.OutputText);
        store.Add(uploaded);

        return TypedResults.Created($"/api/files/{uploaded.Id}", UploadedFileSummary.From(uploaded));
    }

    internal static Ok<UploadedFileSummary[]> List(IUploadedFileStore store) =>
        TypedResults.Ok(store.ListNewestFirst().Select(UploadedFileSummary.From).ToArray());

    internal static Results<FileContentHttpResult, ProblemHttpResult> Download(Guid id, IUploadedFileStore store)
    {
        if (!store.TryGet(id, out var file))
        {
            return TypedResults.Problem(
                title: "File not found",
                detail: $"No uploaded file has id {id}.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var downloadName = Path.GetFileNameWithoutExtension(file.FileName) + DownloadSuffix;
        return TypedResults.File(Encoding.UTF8.GetBytes(file.OutputText), PlainTextUtf8, downloadName);
    }

    /// <summary>Keeps only the bare file name: no directories, no invalid characters, at most 100 characters (ADR-005).</summary>
    private static string SafeFileName(string uploadedName)
    {
        var bareName = uploadedName.Replace('\\', '/').Split('/')[^1];
        var cleaned = new string([.. bareName.Where(c => !InvalidFileNameChars.Contains(c))]).Trim();

        if (cleaned.Length == 0)
        {
            return FallbackFileName;
        }

        if (cleaned.Length > MaxFileNameLength)
        {
            return cleaned[..MaxFileNameLength];
        }

        return cleaned;
    }
}
