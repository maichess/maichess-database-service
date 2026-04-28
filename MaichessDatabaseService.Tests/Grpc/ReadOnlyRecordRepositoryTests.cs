using MaichessDatabaseService.Adapters;
using MaichessDatabaseService.Domain;
using NSubstitute;
using Xunit;

namespace MaichessDatabaseService.Tests.Grpc;

public sealed class ReadOnlyRecordRepositoryTests
{
    private static DbRecord MakeRecord(string id = "x") =>
        new(id, new Dictionary<string, object?>());

    [Fact]
    public async Task Get_DelegatesToInner()
    {
        IRecordRepository inner = Substitute.For<IRecordRepository>();
        inner.GetAsync("col", "id", Arg.Any<CancellationToken>()).Returns(MakeRecord("id"));

        ReadOnlyRecordRepository repo = new(inner);
        DbRecord? result = await repo.GetAsync("col", "id", default);

        Assert.Equal("id", result?.Id);
    }

    [Fact]
    public async Task List_DelegatesToInner()
    {
        IRecordRepository inner = Substitute.For<IRecordRepository>();
        inner.ListAsync("col", Arg.Any<IReadOnlyDictionary<string, object?>>(), 0, 0, Arg.Any<CancellationToken>())
            .Returns(new List<DbRecord> { MakeRecord("a") });

        ReadOnlyRecordRepository repo = new(inner);
        IReadOnlyList<DbRecord> result = await repo.ListAsync("col", new Dictionary<string, object?>(), 0, 0, default);

        Assert.Single(result);
    }

    [Fact]
    public async Task Count_DelegatesToInner()
    {
        IRecordRepository inner = Substitute.For<IRecordRepository>();
        inner.CountAsync("col", Arg.Any<IReadOnlyDictionary<string, object?>>(), Arg.Any<CancellationToken>())
            .Returns(7L);

        ReadOnlyRecordRepository repo = new(inner);
        long count = await repo.CountAsync("col", new Dictionary<string, object?>(), default);

        Assert.Equal(7L, count);
    }

    [Fact]
    public async Task Insert_ThrowsReadOnlyViolation()
    {
        IRecordRepository inner = Substitute.For<IRecordRepository>();
        ReadOnlyRecordRepository repo = new(inner);

        await Assert.ThrowsAsync<ReadOnlyViolationException>(() =>
            repo.InsertAsync("col", new Dictionary<string, object?>(), default));
    }

    [Fact]
    public async Task Update_ThrowsReadOnlyViolation()
    {
        IRecordRepository inner = Substitute.For<IRecordRepository>();
        ReadOnlyRecordRepository repo = new(inner);

        await Assert.ThrowsAsync<ReadOnlyViolationException>(() =>
            repo.UpdateAsync("col", "id", new Dictionary<string, object?>(), default));
    }

    [Fact]
    public async Task Delete_ThrowsReadOnlyViolation()
    {
        IRecordRepository inner = Substitute.For<IRecordRepository>();
        ReadOnlyRecordRepository repo = new(inner);

        await Assert.ThrowsAsync<ReadOnlyViolationException>(() =>
            repo.DeleteAsync("col", "id", default));
    }

    [Fact]
    public async Task DeleteWhere_ThrowsReadOnlyViolation()
    {
        IRecordRepository inner = Substitute.For<IRecordRepository>();
        ReadOnlyRecordRepository repo = new(inner);

        await Assert.ThrowsAsync<ReadOnlyViolationException>(() =>
            repo.DeleteWhereAsync("col", new Dictionary<string, object?>(), default));
    }
}
