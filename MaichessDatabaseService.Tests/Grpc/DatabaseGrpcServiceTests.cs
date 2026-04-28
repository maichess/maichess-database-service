using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Maichess.Database.V1;
using MaichessDatabaseService.Domain;
using MaichessDatabaseService.Grpc;
using MaichessDatabaseService.Tests.Support;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace MaichessDatabaseService.Tests.Grpc;

public sealed class DatabaseGrpcServiceTests
{
    private static DbRecord MakeRecord(string id = "abc", Dictionary<string, object?>? fields = null) =>
        new(id, fields ?? new Dictionary<string, object?> { ["name"] = "test" });

    // --- Get ---

    [Fact]
    public async Task Get_ExistingRecord_ReturnsRecord()
    {
        IRecordRepository repo = Substitute.For<IRecordRepository>();
        repo.GetAsync("col", "abc", Arg.Any<CancellationToken>()).Returns(MakeRecord("abc"));

        DatabaseGrpcService svc = new(repo);
        GetResponse response = await svc.Get(
            new GetRequest { Collection = "col", Id = "abc" },
            TestServerCallContext.Create());

        Assert.Equal("abc", response.Record.Fields["id"].StringValue);
    }

    [Fact]
    public async Task Get_MissingRecord_ReturnsNotFound()
    {
        IRecordRepository repo = Substitute.For<IRecordRepository>();
        repo.GetAsync("col", "missing", Arg.Any<CancellationToken>()).Returns((DbRecord?)null);

        DatabaseGrpcService svc = new(repo);
        RpcException ex = await Assert.ThrowsAsync<RpcException>(() =>
            svc.Get(new GetRequest { Collection = "col", Id = "missing" }, TestServerCallContext.Create()));

        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task Get_EmptyCollection_ReturnsInvalidArgument()
    {
        IRecordRepository repo = Substitute.For<IRecordRepository>();
        DatabaseGrpcService svc = new(repo);

        RpcException ex = await Assert.ThrowsAsync<RpcException>(() =>
            svc.Get(new GetRequest { Collection = string.Empty, Id = "x" }, TestServerCallContext.Create()));

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    // --- List ---

    [Fact]
    public async Task List_NoFilter_ReturnsAllRecords()
    {
        IRecordRepository repo = Substitute.For<IRecordRepository>();
        repo.ListAsync("col", Arg.Any<IReadOnlyDictionary<string, object?>>(), 0, 0, Arg.Any<CancellationToken>())
            .Returns(new List<DbRecord> { MakeRecord("1"), MakeRecord("2") });

        DatabaseGrpcService svc = new(repo);
        ListResponse response = await svc.List(
            new ListRequest { Collection = "col" },
            TestServerCallContext.Create());

        Assert.Equal(2, response.Records.Count);
    }

    [Fact]
    public async Task List_WithFilter_PassesFilterToRepository()
    {
        IRecordRepository repo = Substitute.For<IRecordRepository>();
        repo.ListAsync("col", Arg.Any<IReadOnlyDictionary<string, object?>>(), 0, 0, Arg.Any<CancellationToken>())
            .Returns(new List<DbRecord>());

        Struct filter = new();
        filter.Fields["status"] = Value.ForString("active");

        DatabaseGrpcService svc = new(repo);
        await svc.List(new ListRequest { Collection = "col", Filter = filter }, TestServerCallContext.Create());

        await repo.Received(1).ListAsync(
            "col",
            Arg.Is<IReadOnlyDictionary<string, object?>>(d => d.ContainsKey("status")),
            0, 0,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task List_EmptyCollection_ReturnsInvalidArgument()
    {
        IRecordRepository repo = Substitute.For<IRecordRepository>();
        DatabaseGrpcService svc = new(repo);

        RpcException ex = await Assert.ThrowsAsync<RpcException>(() =>
            svc.List(new ListRequest { Collection = string.Empty }, TestServerCallContext.Create()));

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    // --- Insert ---

    [Fact]
    public async Task Insert_ValidRecord_ReturnsInsertedRecord()
    {
        IRecordRepository repo = Substitute.For<IRecordRepository>();
        repo.InsertAsync("col", Arg.Any<IReadOnlyDictionary<string, object?>>(), Arg.Any<CancellationToken>())
            .Returns(MakeRecord("new-id"));

        Struct record = new();
        record.Fields["name"] = Value.ForString("test");

        DatabaseGrpcService svc = new(repo);
        InsertResponse response = await svc.Insert(
            new InsertRequest { Collection = "col", Record = record },
            TestServerCallContext.Create());

        Assert.Equal("new-id", response.Record.Fields["id"].StringValue);
    }

    [Fact]
    public async Task Insert_ReadOnly_ReturnsPermissionDenied()
    {
        IRecordRepository repo = Substitute.For<IRecordRepository>();
        repo.When(r => r.InsertAsync(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, object?>>(), Arg.Any<CancellationToken>()))
            .Throw(new ReadOnlyViolationException());

        DatabaseGrpcService svc = new(repo);
        RpcException ex = await Assert.ThrowsAsync<RpcException>(() =>
            svc.Insert(new InsertRequest { Collection = "col", Record = new Struct() }, TestServerCallContext.Create()));

        Assert.Equal(StatusCode.PermissionDenied, ex.StatusCode);
    }

    [Fact]
    public async Task Insert_DuplicateKey_ReturnsAlreadyExists()
    {
        IRecordRepository repo = Substitute.For<IRecordRepository>();
        repo.When(r => r.InsertAsync(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, object?>>(), Arg.Any<CancellationToken>()))
            .Throw(new AlreadyExistsException("dup"));

        DatabaseGrpcService svc = new(repo);
        RpcException ex = await Assert.ThrowsAsync<RpcException>(() =>
            svc.Insert(new InsertRequest { Collection = "col", Record = new Struct() }, TestServerCallContext.Create()));

        Assert.Equal(StatusCode.AlreadyExists, ex.StatusCode);
    }

    [Fact]
    public async Task Insert_EmptyCollection_ReturnsInvalidArgument()
    {
        IRecordRepository repo = Substitute.For<IRecordRepository>();
        DatabaseGrpcService svc = new(repo);

        RpcException ex = await Assert.ThrowsAsync<RpcException>(() =>
            svc.Insert(new InsertRequest { Collection = string.Empty, Record = new Struct() }, TestServerCallContext.Create()));

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    // --- Update ---

    [Fact]
    public async Task Update_ExistingRecord_ReturnsUpdatedRecord()
    {
        IRecordRepository repo = Substitute.For<IRecordRepository>();
        repo.UpdateAsync("col", "id1", Arg.Any<IReadOnlyDictionary<string, object?>>(), Arg.Any<CancellationToken>())
            .Returns(MakeRecord("id1", new Dictionary<string, object?> { ["name"] = "updated" }));

        Struct fields = new();
        fields.Fields["name"] = Value.ForString("updated");

        DatabaseGrpcService svc = new(repo);
        UpdateResponse response = await svc.Update(
            new UpdateRequest { Collection = "col", Id = "id1", Fields = fields },
            TestServerCallContext.Create());

        Assert.Equal("updated", response.Record.Fields["name"].StringValue);
    }

    [Fact]
    public async Task Update_MissingRecord_ReturnsNotFound()
    {
        IRecordRepository repo = Substitute.For<IRecordRepository>();
        repo.When(r => r.UpdateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, object?>>(), Arg.Any<CancellationToken>()))
            .Throw(new NotFoundException("not found"));

        DatabaseGrpcService svc = new(repo);
        RpcException ex = await Assert.ThrowsAsync<RpcException>(() =>
            svc.Update(new UpdateRequest { Collection = "col", Id = "missing", Fields = new Struct() }, TestServerCallContext.Create()));

        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task Update_ReadOnly_ReturnsPermissionDenied()
    {
        IRecordRepository repo = Substitute.For<IRecordRepository>();
        repo.When(r => r.UpdateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, object?>>(), Arg.Any<CancellationToken>()))
            .Throw(new ReadOnlyViolationException());

        DatabaseGrpcService svc = new(repo);
        RpcException ex = await Assert.ThrowsAsync<RpcException>(() =>
            svc.Update(new UpdateRequest { Collection = "col", Id = "id", Fields = new Struct() }, TestServerCallContext.Create()));

        Assert.Equal(StatusCode.PermissionDenied, ex.StatusCode);
    }

    [Fact]
    public async Task Update_DuplicateKey_ReturnsAlreadyExists()
    {
        IRecordRepository repo = Substitute.For<IRecordRepository>();
        repo.When(r => r.UpdateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, object?>>(), Arg.Any<CancellationToken>()))
            .Throw(new AlreadyExistsException("dup"));

        DatabaseGrpcService svc = new(repo);
        RpcException ex = await Assert.ThrowsAsync<RpcException>(() =>
            svc.Update(new UpdateRequest { Collection = "col", Id = "id", Fields = new Struct() }, TestServerCallContext.Create()));

        Assert.Equal(StatusCode.AlreadyExists, ex.StatusCode);
    }

    [Fact]
    public async Task Update_EmptyCollection_ReturnsInvalidArgument()
    {
        IRecordRepository repo = Substitute.For<IRecordRepository>();
        DatabaseGrpcService svc = new(repo);

        RpcException ex = await Assert.ThrowsAsync<RpcException>(() =>
            svc.Update(new UpdateRequest { Collection = string.Empty, Id = "id", Fields = new Struct() }, TestServerCallContext.Create()));

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_ExistingRecord_Succeeds()
    {
        IRecordRepository repo = Substitute.For<IRecordRepository>();
        repo.DeleteAsync("col", "id1", Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        DatabaseGrpcService svc = new(repo);
        DeleteResponse response = await svc.Delete(
            new DeleteRequest { Collection = "col", Id = "id1" },
            TestServerCallContext.Create());

        Assert.NotNull(response);
    }

    [Fact]
    public async Task Delete_MissingRecord_ReturnsNotFound()
    {
        IRecordRepository repo = Substitute.For<IRecordRepository>();
        repo.When(r => r.DeleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()))
            .Throw(new NotFoundException("not found"));

        DatabaseGrpcService svc = new(repo);
        RpcException ex = await Assert.ThrowsAsync<RpcException>(() =>
            svc.Delete(new DeleteRequest { Collection = "col", Id = "missing" }, TestServerCallContext.Create()));

        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task Delete_ReadOnly_ReturnsPermissionDenied()
    {
        IRecordRepository repo = Substitute.For<IRecordRepository>();
        repo.When(r => r.DeleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()))
            .Throw(new ReadOnlyViolationException());

        DatabaseGrpcService svc = new(repo);
        RpcException ex = await Assert.ThrowsAsync<RpcException>(() =>
            svc.Delete(new DeleteRequest { Collection = "col", Id = "id" }, TestServerCallContext.Create()));

        Assert.Equal(StatusCode.PermissionDenied, ex.StatusCode);
    }

    [Fact]
    public async Task Delete_EmptyCollection_ReturnsInvalidArgument()
    {
        IRecordRepository repo = Substitute.For<IRecordRepository>();
        DatabaseGrpcService svc = new(repo);

        RpcException ex = await Assert.ThrowsAsync<RpcException>(() =>
            svc.Delete(new DeleteRequest { Collection = string.Empty, Id = "id" }, TestServerCallContext.Create()));

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }
}
