using System.Diagnostics.CodeAnalysis;

namespace CashRegister.Features.Change.Files;

/// <summary>Where processed files are kept. In memory for v1; a database in v2 (ADR-002).</summary>
internal interface IUploadedFileStore
{
    void Add(UploadedFile file);

    bool TryGet(Guid id, [MaybeNullWhen(false)] out UploadedFile file);

    IReadOnlyList<UploadedFile> ListNewestFirst();
}
