using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace CashRegister.Features.Change.Files;

/// <summary>Thread-safe, process-lifetime store; everything is lost on restart (NFR-005).</summary>
internal sealed class InMemoryUploadedFileStore : IUploadedFileStore
{
    private readonly ConcurrentDictionary<Guid, UploadedFile> _files = new();

    public void Add(UploadedFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        _files[file.Id] = file;
    }

    public bool TryGet(Guid id, [MaybeNullWhen(false)] out UploadedFile file) =>
        _files.TryGetValue(id, out file);

    public IReadOnlyList<UploadedFile> ListNewestFirst() =>
        [.. _files.Values.OrderByDescending(f => f.UploadedAt)];
}
