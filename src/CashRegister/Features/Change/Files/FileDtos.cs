namespace CashRegister.Features.Change.Files;

/// <summary>An uploaded-file entry as shown in the UI list (AC-027). The output text is fetched separately.</summary>
public sealed record UploadedFileSummary(Guid Id, string FileName, DateTimeOffset UploadedAt, int LineCount, int ErrorLineCount)
{
    internal static UploadedFileSummary From(UploadedFile file) =>
        new(file.Id, file.FileName, file.UploadedAt, file.LineCount, file.ErrorLineCount);
}
