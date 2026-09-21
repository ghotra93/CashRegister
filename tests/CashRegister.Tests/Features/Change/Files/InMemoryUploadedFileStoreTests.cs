using CashRegister.Features.Change.Files;

namespace CashRegister.Tests.Features.Change.Files;

public sealed class InMemoryUploadedFileStoreTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);
    private readonly InMemoryUploadedFileStore _store = new();

    private static UploadedFile FileAt(DateTimeOffset uploadedAt, string name = "input.txt") =>
        new(Guid.NewGuid(), name, uploadedAt, LineCount: 3, ErrorLineCount: 0, OutputText: "3 pennies");

    [Fact]
    [Trait("AC", "AC-028")]
    public void TryGet_AddedFile_ReturnsIt()
    {
        var file = FileAt(Noon);
        _store.Add(file);

        _store.TryGet(file.Id, out var found).ShouldBeTrue();
        found.ShouldBe(file);
    }

    [Fact]
    public void TryGet_UnknownId_ReturnsFalse()
    {
        _store.TryGet(Guid.NewGuid(), out _).ShouldBeFalse();
    }

    [Fact]
    [Trait("AC", "AC-027")]
    public void ListNewestFirst_OrdersByUploadTimeDescending()
    {
        var oldest = FileAt(Noon, "a.txt");
        var newest = FileAt(Noon.AddMinutes(2), "c.txt");
        var middle = FileAt(Noon.AddMinutes(1), "b.txt");
        _store.Add(oldest);
        _store.Add(newest);
        _store.Add(middle);

        _store.ListNewestFirst().Select(f => f.FileName).ShouldBe(["c.txt", "b.txt", "a.txt"]);
    }

    [Fact]
    public void ListNewestFirst_Empty_IsEmpty()
    {
        _store.ListNewestFirst().ShouldBeEmpty();
    }

    [Fact]
    public async Task Add_FromManyThreads_KeepsEveryFile()
    {
        await Task.WhenAll(Enumerable.Range(0, 200).Select(i => Task.Run(() => _store.Add(FileAt(Noon.AddSeconds(i))))));

        _store.ListNewestFirst().Count.ShouldBe(200);
    }
}
