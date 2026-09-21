namespace CashRegister.Features.Change.Files;

/// <summary>A processed file kept for listing and download (AC-027, AC-028).</summary>
internal sealed record UploadedFile(
    Guid Id,
    string FileName,
    DateTimeOffset UploadedAt,
    int LineCount,
    int ErrorLineCount,
    string OutputText);
